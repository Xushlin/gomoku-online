using Gomoku.Domain.Enums;
using Gomoku.Domain.Exceptions;
using Gomoku.Domain.Users;
using Gomoku.Domain.ValueObjects;
using DomainMove = Gomoku.Domain.ValueObjects.Move;
using SubMove = Gomoku.Domain.Rooms.Move;

namespace Gomoku.Domain.Rooms;

/// <summary>一次成功落子的领域级结果,<see cref="Room.PlayMove"/> 返回给 handler。</summary>
public sealed record MoveOutcome(SubMove Move, GameResult Result);

/// <summary>一次成功催促的领域级结果。<see cref="UrgedUser"/> 是被催的玩家。</summary>
public sealed record UrgeOutcome(UserId UrgedUser);

/// <summary>
/// 对局非连五路径(<see cref="Room.Resign"/> / <see cref="Room.TimeOutCurrentTurn"/>)的领域结果。
/// <see cref="Result"/> 为胜方色;平局不可能通过认输 / 超时触发,故 <see cref="WinnerUserId"/> 非空。
/// </summary>
public sealed record GameEndOutcome(GameResult Result, UserId? WinnerUserId);

/// <summary>
/// 房间聚合根:承载玩家、围观者、对局、聊天、催促时间戳与生命周期状态机。
/// 所有对 <see cref="Game"/> / <see cref="ChatMessage"/> / 围观者的修改 MUST 通过本类的领域方法。
/// </summary>
public sealed class Room
{
    private const int MinNameLength = 3;
    private const int MaxNameLength = 50;
    private const int MaxChatContentLength = 500;

    private readonly List<RoomSpectator> _spectators = new();
    private readonly List<ChatMessage> _chatMessages = new();

    /// <summary>房间主键。</summary>
    public RoomId Id { get; private set; }

    /// <summary>房间名(trim 后 3–50 字符)。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>创建者 / Host。当前规则下默认也是黑方玩家。</summary>
    public UserId HostUserId { get; private set; }

    /// <summary>黑方玩家(创建时即 Host)。</summary>
    public UserId BlackPlayerId { get; private set; }

    /// <summary>白方玩家;Waiting 状态下为 <c>null</c>。</summary>
    public UserId? WhitePlayerId { get; private set; }

    /// <summary>生命周期状态。</summary>
    public RoomStatus Status { get; private set; }

    /// <summary>创建时间(UTC)。</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>最近一次催促时间(UTC);从未催促过则 <c>null</c>。</summary>
    public DateTime? LastUrgeAt { get; private set; }

    /// <summary>最近一次催促的发起者。</summary>
    public UserId? LastUrgeByUserId { get; private set; }

    /// <summary>对局子实体;Waiting 状态下为 <c>null</c>。</summary>
    public Game? Game { get; private set; }

    /// <summary>围观者的用户 Id 集合(只读投影,屏蔽内部 <see cref="RoomSpectator"/> 实体)。</summary>
    public IReadOnlyCollection<UserId> Spectators =>
        _spectators.Select(s => s.UserId).ToList().AsReadOnly();

    /// <summary>历史聊天消息(只读视图)。</summary>
    public IReadOnlyCollection<ChatMessage> ChatMessages => _chatMessages;

    // EF 物化用。
    private Room() { }

    /// <summary>
    /// 创建一个新房间。创建者默认成为 Host 与黑方;状态为 <see cref="RoomStatus.Waiting"/>。
    /// </summary>
    /// <exception cref="InvalidRoomNameException">名称为空 / 空白 / 长度不在 [3..50]。</exception>
    public static Room Create(RoomId id, string name, UserId hostUserId, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidRoomNameException("Room name must not be null or whitespace.");
        }

        var trimmed = name.Trim();
        if (trimmed.Length < MinNameLength || trimmed.Length > MaxNameLength)
        {
            throw new InvalidRoomNameException(
                $"Room name length {trimmed.Length} is out of range [{MinNameLength}..{MaxNameLength}].");
        }

        return new Room
        {
            Id = id,
            Name = trimmed,
            HostUserId = hostUserId,
            BlackPlayerId = hostUserId,
            WhitePlayerId = null,
            Status = RoomStatus.Waiting,
            CreatedAt = createdAt,
        };
    }

    /// <summary>
    /// 第二位玩家加入为白方,对局启动。若加入者此前是围观者,先从 <see cref="Spectators"/> 移除。
    /// </summary>
    public void JoinAsPlayer(UserId userId, DateTime now)
    {
        if (Status != RoomStatus.Waiting)
        {
            throw new RoomNotWaitingException(
                $"Cannot join as player when room status is {Status}.");
        }

        if (userId == BlackPlayerId)
        {
            throw new AlreadyInRoomException($"User {userId.Value} is already the black player.");
        }

        var existing = _spectators.FirstOrDefault(s => s.UserId == userId);
        if (existing is not null)
        {
            _spectators.Remove(existing);
        }

        if (WhitePlayerId is not null)
        {
            throw new RoomFullException("Room already has two players.");
        }

        WhitePlayerId = userId;
        TransitionStatus(RoomStatus.Playing);
        Game = new Game(Id, now);
    }

    /// <summary>
    /// 玩家 / 围观者离开房间。Waiting 状态下 Host 不得"静默离开"(请用解散房间接口,本变更未实现)。
    /// 对局中的玩家离开视为"离席",不改变 Game 状态 —— 超时 / 认输留给后续变更。
    /// </summary>
    public void Leave(UserId userId, DateTime now)
    {
        _ = now; // 目前未用时间戳(为未来"离席时间"留参数位)

        var spectator = _spectators.FirstOrDefault(s => s.UserId == userId);
        if (spectator is not null)
        {
            _spectators.Remove(spectator);
            return;
        }

        var isPlayer = userId == BlackPlayerId || userId == WhitePlayerId;
        if (!isPlayer)
        {
            throw new NotInRoomException($"User {userId.Value} is not in this room.");
        }

        if (Status == RoomStatus.Waiting && userId == HostUserId)
        {
            throw new HostCannotLeaveWaitingRoomException(
                "Host cannot leave a Waiting room; dissolve it instead.");
        }

        // 对局中玩家离席 / Finished 后任何人离开:不改 Game 状态,也不改 Status;
        // 玩家关系保留(Game.WinnerUserId 等仍需要引用到)。认输 / 超时由后续变更覆盖。
    }

    /// <summary>
    /// 由 Host 解散一个 <see cref="RoomStatus.Waiting"/> 状态的房间。本方法**只做校验**:
    /// 身份(是否 Host)与状态(是否 Waiting);通过则返回,不修改 <c>Room</c> 任何字段。
    /// 物理删除发生在仓储层(<c>IRoomRepository.DeleteAsync</c>),聚合自身不持有"Dissolved"状态
    /// —— Waiting 房的全部状态(名字 + 围观者 + 可能的聊天)随房间一并 Cascade 删除,不保留审计痕迹。
    /// </summary>
    /// <exception cref="NotRoomHostException"><paramref name="senderId"/> 不是 <see cref="HostUserId"/>。</exception>
    /// <exception cref="RoomNotWaitingException"><see cref="Status"/> 不是 <see cref="RoomStatus.Waiting"/>。</exception>
    public void Dissolve(UserId senderId)
    {
        if (senderId != HostUserId)
        {
            throw new NotRoomHostException(
                $"User {senderId.Value} is not the host of room {Id.Value}; only the host may dissolve.");
        }

        if (Status != RoomStatus.Waiting)
        {
            throw new RoomNotWaitingException(
                $"Cannot dissolve room when status is {Status}; dissolve is only for Waiting rooms.");
        }

        // 两项校验通过 —— 方法到此结束,聚合状态保持不变。
    }

    /// <summary>加入围观者集合。玩家不可围观自己的对局。重复加入幂等。</summary>
    public void JoinAsSpectator(UserId userId)
    {
        if (userId == BlackPlayerId || userId == WhitePlayerId)
        {
            throw new PlayerCannotSpectateException(
                $"User {userId.Value} is a player in this room and cannot spectate.");
        }

        if (_spectators.Any(s => s.UserId == userId))
        {
            return; // 幂等
        }

        // JoinedAt 先用一个静态占位 —— 真实时间由 handler 通过 IDateTimeProvider 决定时,
        // 不影响外部行为(观众列表只看 UserId);若未来需要 JoinedAt 供分析,改方法签名接收时间。
        _spectators.Add(new RoomSpectator(Id, userId, DateTime.MinValue));
    }

    /// <summary>从围观者集合离开。若用户不在围观者中,抛 <see cref="NotSpectatingException"/>。</summary>
    public void LeaveAsSpectator(UserId userId)
    {
        var entry = _spectators.FirstOrDefault(s => s.UserId == userId);
        if (entry is null)
        {
            throw new NotSpectatingException($"User {userId.Value} is not spectating this room.");
        }
        _spectators.Remove(entry);
    }

    /// <summary>
    /// 落子领域入口。按 spec 8 步执行:状态校验 → 身份校验 → 回合校验 →
    /// <see cref="Board"/> 合法性 → 记录 <see cref="SubMove"/> → 翻转回合 →
    /// 判胜 → 可能转入 Finished。
    /// </summary>
    public MoveOutcome PlayMove(UserId userId, Position position, DateTime now)
    {
        if (Status != RoomStatus.Playing)
        {
            throw new RoomNotInPlayException(
                $"Cannot play move when room status is {Status}.");
        }

        if (Game is null)
        {
            // Playing 状态时 Game 必非空;这是防御性保护。
            throw new RoomNotInPlayException("Room is in Playing state but has no Game instance.");
        }

        Stone playerStone;
        if (userId == BlackPlayerId)
        {
            playerStone = Stone.Black;
        }
        else if (WhitePlayerId is not null && userId == WhitePlayerId.Value)
        {
            playerStone = Stone.White;
        }
        else
        {
            throw new NotAPlayerException($"User {userId.Value} is not a player in this room.");
        }

        if (playerStone != Game.CurrentTurn)
        {
            throw new NotYourTurnException(
                $"It is not {playerStone}'s turn; current turn is {Game.CurrentTurn}.");
        }

        // 让 Board 做越界 / 重复落子判定;若非法抛 InvalidMoveException 向上冒泡,
        // Game 的 Moves 在此之前尚未追加,状态不变。
        var board = Game.ReplayBoard();
        var result = board.PlaceStone(new DomainMove(position, playerStone));

        var appended = Game.RecordMove(position, playerStone, now);

        if (result != GameResult.Ongoing)
        {
            var winnerId = result switch
            {
                GameResult.BlackWin => (UserId?)BlackPlayerId,
                GameResult.WhiteWin => WhitePlayerId,
                _ => null,
            };
            Game.FinishWith(result, winnerId, GameEndReason.Connected5, now);
            TransitionStatus(RoomStatus.Finished);
        }

        return new MoveOutcome(appended, result);
    }

    /// <summary>
    /// 玩家主动认输。允许**任意回合**调用(包括对手回合);对局立即结束,对手胜。
    /// </summary>
    /// <exception cref="RoomNotInPlayException">房间不在 <see cref="RoomStatus.Playing"/>。</exception>
    /// <exception cref="NotAPlayerException"><paramref name="userId"/> 不是 Black / White 玩家。</exception>
    public GameEndOutcome Resign(UserId userId, DateTime now)
    {
        if (Status != RoomStatus.Playing)
        {
            throw new RoomNotInPlayException(
                $"Cannot resign when room status is {Status}.");
        }

        if (Game is null)
        {
            throw new RoomNotInPlayException("Room is in Playing state but has no Game instance.");
        }

        UserId opponentUserId;
        GameResult opponentResult;
        if (userId == BlackPlayerId)
        {
            opponentUserId = WhitePlayerId!.Value;
            opponentResult = GameResult.WhiteWin;
        }
        else if (WhitePlayerId is not null && userId == WhitePlayerId.Value)
        {
            opponentUserId = BlackPlayerId;
            opponentResult = GameResult.BlackWin;
        }
        else
        {
            throw new NotAPlayerException(
                $"User {userId.Value} is not a player in this room and cannot resign.");
        }

        Game.FinishWith(opponentResult, opponentUserId, GameEndReason.Resigned, now);
        TransitionStatus(RoomStatus.Finished);
        return new GameEndOutcome(opponentResult, opponentUserId);
    }

    /// <summary>
    /// 若当前回合的玩家超过 <paramref name="turnTimeoutSeconds"/> 未落子,则判其负、对手胜。
    /// 方法**重新计算** <c>lastActivity = Moves.Last().PlayedAt ?? Game.StartedAt</c>;
    /// 若 <c>(now - lastActivity).TotalSeconds &lt; turnTimeoutSeconds</c> 抛
    /// <see cref="TurnNotTimedOutException"/>(防 worker 与玩家落子的竞态:worker poll 时超时,
    /// 但到 handler 执行时对手恰好落了子推新了 lastActivity)。调用方(worker handler)
    /// **MUST** 捕获 <see cref="TurnNotTimedOutException"/> 并吞掉,下轮轮询不会再命中该房间。
    /// </summary>
    /// <exception cref="RoomNotInPlayException">房间不在 Playing。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="turnTimeoutSeconds"/> &lt; 1。</exception>
    /// <exception cref="TurnNotTimedOutException">尚未到超时阈值。</exception>
    public GameEndOutcome TimeOutCurrentTurn(DateTime now, int turnTimeoutSeconds)
    {
        if (Status != RoomStatus.Playing)
        {
            throw new RoomNotInPlayException(
                $"Cannot time out when room status is {Status}.");
        }

        if (Game is null)
        {
            throw new RoomNotInPlayException("Room is in Playing state but has no Game instance.");
        }

        if (turnTimeoutSeconds < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(turnTimeoutSeconds), turnTimeoutSeconds, "Timeout seconds must be at least 1.");
        }

        var lastMove = Game.Moves.OrderBy(m => m.Ply).LastOrDefault();
        var lastActivity = lastMove?.PlayedAt ?? Game.StartedAt;

        if ((now - lastActivity).TotalSeconds < turnTimeoutSeconds)
        {
            throw new TurnNotTimedOutException(
                $"Current turn has not yet exceeded {turnTimeoutSeconds}s (elapsed {(now - lastActivity).TotalSeconds}s).");
        }

        UserId winnerUserId;
        GameResult winnerResult;
        if (Game.CurrentTurn == Stone.Black)
        {
            winnerUserId = WhitePlayerId!.Value;
            winnerResult = GameResult.WhiteWin;
        }
        else
        {
            winnerUserId = BlackPlayerId;
            winnerResult = GameResult.BlackWin;
        }

        Game.FinishWith(winnerResult, winnerUserId, GameEndReason.TurnTimeout, now);
        TransitionStatus(RoomStatus.Finished);
        return new GameEndOutcome(winnerResult, winnerUserId);
    }

    /// <summary>
    /// 在本房间发表一条聊天消息。按频道校验发送者权限、内容规范化与长度。
    /// </summary>
    public ChatMessage PostChatMessage(
        UserId senderId,
        string senderUsername,
        string rawContent,
        ChatChannel channel,
        DateTime now)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new InvalidChatContentException("Chat content must not be null or whitespace.");
        }

        var content = rawContent.Trim();
        if (content.Length == 0 || content.Length > MaxChatContentLength)
        {
            throw new InvalidChatContentException(
                $"Chat content length {content.Length} is out of range [1..{MaxChatContentLength}].");
        }

        var isPlayer = senderId == BlackPlayerId || senderId == WhitePlayerId;
        var isSpectator = _spectators.Any(s => s.UserId == senderId);
        if (!isPlayer && !isSpectator)
        {
            throw new NotInRoomException($"User {senderId.Value} is not in this room.");
        }

        if (channel == ChatChannel.Spectator && isPlayer)
        {
            throw new PlayerCannotPostSpectatorChannelException(
                "Players cannot post to the spectator channel.");
        }

        var message = new ChatMessage(Id, senderId, senderUsername, content, channel, now);
        _chatMessages.Add(message);
        return message;
    }

    /// <summary>催促对手下棋。仅 Playing 状态、仅玩家、仅对手回合时可调,冷却 <paramref name="cooldownSeconds"/> 秒。</summary>
    public UrgeOutcome UrgeOpponent(UserId senderId, DateTime now, int cooldownSeconds)
    {
        if (Status != RoomStatus.Playing)
        {
            throw new RoomNotInPlayException(
                $"Cannot urge when room status is {Status}.");
        }

        if (Game is null)
        {
            throw new RoomNotInPlayException("Room is in Playing state but has no Game instance.");
        }

        Stone senderStone;
        UserId urgedUser;
        if (senderId == BlackPlayerId)
        {
            senderStone = Stone.Black;
            urgedUser = WhitePlayerId!.Value;
        }
        else if (WhitePlayerId is not null && senderId == WhitePlayerId.Value)
        {
            senderStone = Stone.White;
            urgedUser = BlackPlayerId;
        }
        else
        {
            throw new NotAPlayerException(
                $"User {senderId.Value} is not a player and cannot urge.");
        }

        if (senderStone == Game.CurrentTurn)
        {
            throw new NotOpponentsTurnException(
                "It is your own turn; nothing to urge.");
        }

        if (LastUrgeAt is not null
            && (now - LastUrgeAt.Value).TotalSeconds < cooldownSeconds)
        {
            throw new UrgeTooFrequentException(
                $"Urge cooldown not elapsed; {cooldownSeconds}s required.");
        }

        LastUrgeAt = now;
        LastUrgeByUserId = senderId;
        return new UrgeOutcome(urgedUser);
    }

    private void TransitionStatus(RoomStatus target)
    {
        var ok = (Status, target) switch
        {
            (RoomStatus.Waiting, RoomStatus.Playing) => true,
            (RoomStatus.Playing, RoomStatus.Finished) => true,
            _ => false,
        };

        if (!ok)
        {
            throw new InvalidRoomStatusTransitionException(
                $"Illegal transition {Status} -> {target}.");
        }

        Status = target;
    }
}
