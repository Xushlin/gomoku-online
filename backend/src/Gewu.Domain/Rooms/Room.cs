using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;
using DomainMove = Gewu.Domain.ValueObjects.Move;
using SubMove = Gewu.Domain.Rooms.Move;

namespace Gewu.Domain.Rooms;

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
/// 一次超时处理的结果:**要么替他走了一步,要么判他负结束了对局** —— 恰好一个。
/// <para>
/// 两种结果不能合成一个,因为调用方要做的事不同:走了一步要广播 <c>MoveMade</c>,
/// 结束了要广播 <c>GameEnded</c>。用两个可空字段加一条"恰好一个"的不变量,而不是一个
/// 带标志位的记录 —— 与 <c>MoveIntent</c> 的"位置类 / 文本类"同一种形状,同一个理由:
/// 一个说不通的组合在构造时就不成立。
/// </para>
/// </summary>
/// <param name="Move">替他走的那一步;判负时为 <c>null</c>。</param>
/// <param name="Ended">判负的结果;走了一步时为 <c>null</c>。</param>
public sealed record TurnTimeoutOutcome(MoveOutcome? Move, GameEndOutcome? Ended)
{
    /// <summary>替这个座位走了一步。</summary>
    /// <param name="move">那一步。</param>
    public static TurnTimeoutOutcome Played(MoveOutcome move) => new(move, null);

    /// <summary>判他负,对局结束。</summary>
    /// <param name="ended">结束的结果。</param>
    public static TurnTimeoutOutcome Finished(GameEndOutcome ended) => new(null, ended);

    /// <summary>替他走的那一步;判负时为 <c>null</c>。</summary>
    public MoveOutcome? Move { get; } = Validate(Move, Ended).move;

    /// <summary>判负的结果;走了一步时为 <c>null</c>。</summary>
    public GameEndOutcome? Ended { get; } = Validate(Move, Ended).ended;

    private static (MoveOutcome? move, GameEndOutcome? ended) Validate(
        MoveOutcome? move, GameEndOutcome? ended)
    {
        if ((move is null) == (ended is null))
        {
            throw new System.InvalidOperationException(
                "A turn timeout either played a move or ended the game, never both or neither.");
        }
        return (move, ended);
    }
}

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

    /// <summary>
    /// 本房间玩的是哪个棋种。字符串而非枚举 —— 新增棋种的全部意义就在于不必修改一个
    /// 共享类型,与游戏目录、<c>IPuzzleRules</c> 注册表的选择一致。
    /// </summary>
    public string GameKey { get; private set; } = string.Empty;

    /// <summary>创建者 / Host。当前规则下默认也是黑方玩家。</summary>
    private readonly List<RoomSeat> _seats = [];

    public UserId HostUserId { get; private set; }

    /// <summary>黑方玩家(创建时即 Host)。</summary>
    /// <summary>先手座位号 —— 目前就是 <c>BlackPlayerId</c> 那个座位。</summary>
    public static readonly int FirstSeat = 0;

    /// <summary>后手座位号。</summary>
    public static readonly int SecondSeat = 1;

    /// <summary>
    /// 先手座位上的玩家 —— **两人棋种的"黑方"就是 0 号座位**。
    /// <para>
    /// 这是**派生**的,不是字段:座位存在 <see cref="Seats"/> 里,只有一份。名字留着,是因为
    /// 87 处调用点读的正是"黑方是谁",而对两人棋种那句话仍然成立 —— 与 <c>Stone</c> 的处理同一条:
    /// 名字留着,含义降到它真正成立的那一层。
    /// </para>
    /// <para>
    /// **牌类棋种 MUST NOT 用这两个名字** —— 三个座位没有"黑白",用 <see cref="PlayerAt"/>。
    /// </para>
    /// </summary>
    public UserId BlackPlayerId => _seats.Single(x => x.Index == FirstSeat).UserId;

    /// <summary>后手座位上的玩家;还没人坐时为 <c>null</c>。见 <see cref="BlackPlayerId"/>。</summary>
    public UserId? WhitePlayerId =>
        _seats.SingleOrDefault(x => x.Index == SecondSeat)?.UserId;

    /// <summary>本房间的座位,按座位号升序。</summary>
    public IReadOnlyList<RoomSeat> Seats =>
        _seats.OrderBy(x => x.Index).ToList();

    /// <summary>第 <paramref name="index"/> 号座位上的玩家;空座位为 <c>null</c>。</summary>
    /// <param name="index">座位号。</param>
    public UserId? PlayerAt(int index) =>
        _seats.SingleOrDefault(x => x.Index == index)?.UserId;

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
    public static Room Create(
        RoomId id, string name, UserId hostUserId, DateTime createdAt, string gameKey)
    {
        if (string.IsNullOrWhiteSpace(gameKey))
        {
            throw new ArgumentException("Game key must be non-empty.", nameof(gameKey));
        }

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

        var room = new Room
        {
            Id = id,
            Name = trimmed,
            GameKey = gameKey,
            HostUserId = hostUserId,
            Status = RoomStatus.Waiting,
            CreatedAt = createdAt,
        };
        room._seats.Add(new RoomSeat(id, FirstSeat, hostUserId));
        return room;
    }

    /// <summary>
    /// 第二位玩家加入为白方,对局启动。若加入者此前是围观者,先从 <see cref="Spectators"/> 移除。
    /// </summary>
    /// <param name="userId">入座的用户。</param>
    /// <param name="now">当前时间(UTC)。</param>
    /// <param name="rules">本房间棋种的规则 —— 座位数由它给。</param>
    /// <param name="setup">
    /// 本局的服务端侧设置,由调用方造好传入;不需要设置的棋种传 <c>null</c>。
    /// <para>
    /// **没有默认值,而且必须与规则一致。** 默认值会让"忘了传"和"故意不传"在源码里长得
    /// 一模一样(同 <c>fix-spectator-chat-leak</c> 给 <c>ToState</c> 加必填 <c>RoomView</c>
    /// 的理由),而这里更进一步:开局那一刻两者不一致就抛,于是"忘了传"是一个异常,
    /// 不是一局没有牌的斗地主。
    /// </para>
    /// <para>
    /// 由调用方造而不是由本方法从一个种子生成:造它需要熵,而 Domain 不该知道有一个随机源。
    /// 熵的来源是 Application 层已有的 <c>ISeedProvider</c>。这也让测试可复现 —— 传一个
    /// 钉住的设置串,而不是"发了什么算什么"。
    /// </para>
    /// </param>
    public void JoinAsPlayer(UserId userId, DateTime now, IGameRules rules, string? setup)
    {
        if (Status != RoomStatus.Waiting)
        {
            throw new RoomNotWaitingException(
                $"Cannot join as player when room status is {Status}.");
        }

        if (SeatOf(userId) is int taken)
        {
            throw new AlreadyInRoomException(
                $"User {userId.Value} is already seated at {taken}.");
        }

        var existing = _spectators.FirstOrDefault(s => s.UserId == userId);
        if (existing is not null)
        {
            _spectators.Remove(existing);
        }

        if (_seats.Count >= rules.SeatCount)
        {
            throw new RoomFullException(
                $"Room already has all {rules.SeatCount} players.");
        }

        _seats.Add(new RoomSeat(Id, _seats.Count, userId));

        // 坐满才开局。两人棋种下与此前逐步等价(第二个人一坐满就开局);三座位棋种下
        // 第二个人坐进来之后房间**留在 Waiting**。
        if (_seats.Count == rules.SeatCount)
        {
            // 一致性校验发生在**开局那一刻**,而不是每一次入座:否则三人棋种的前两次入座
            // 都得携带一份最终会被丢掉的设置,而那份设置的存在会误导下一个读代码的人。
            var needsSetup = rules is IDealtGameRules;
            if (needsSetup && setup is null)
            {
                throw new MissingGameSetupException(
                    $"'{rules.GameKey}' deals a setup at start; none was supplied.");
            }
            if (!needsSetup && setup is not null)
            {
                // 第二个方向同样要抛:一个把设置传给不需要设置的棋种的调用方,拿着一个错误的
                // 心智模型,而那份设置会被存下来再也没人读。
                throw new MissingGameSetupException(
                    $"'{rules.GameKey}' has no setup, but one was supplied.");
            }

            TransitionStatus(RoomStatus.Playing);
            Game = new Game(Id, now, setup);
        }
    }

    /// <summary>
    /// 在棋局尚未开局时(<see cref="Status"/> = Playing 且 <c>Game.Moves</c> 为空)
    /// 互换 <see cref="BlackPlayerId"/> 与 <see cref="WhitePlayerId"/>。<see cref="HostUserId"/>
    /// 不变(host 仍是房间创建者),<see cref="Game"/>.<c>CurrentTurn</c> 不变(始终是 Black,
    /// 因为黑子先行的规则与"谁坐黑"无关)。
    ///
    /// 主要给 <c>CreateAiRoomCommandHandler</c> 用 —— 真人选择执白时,对 <c>JoinAsPlayer</c>
    /// 后立刻 swap,使 bot 占黑、第 1 步轮到 bot。AI worker 同事务提交后才能看见房间,
    /// 不存在与 swap 的竞争。
    /// </summary>
    /// <exception cref="InvalidOperationException">Status 不是 Playing,或已经有落子。</exception>
    public void SwapPlayers(DateTime now)
    {
        _ = now; // 目前未用时间戳(为未来"swap 时间审计"留参数位)

        if (Status != RoomStatus.Playing)
        {
            throw new InvalidOperationException(
                $"Cannot swap players when room status is {Status}; only valid in Playing.");
        }

        if (Game is null || Game.Moves.Count > 0)
        {
            throw new InvalidOperationException(
                "Cannot swap players after the first move.");
        }

        var first = _seats.Single(x => x.Index == FirstSeat);
        var second = _seats.Single(x => x.Index == SecondSeat);
        _seats.Remove(first);
        _seats.Remove(second);
        _seats.Add(new RoomSeat(Id, FirstSeat, second.UserId));
        _seats.Add(new RoomSeat(Id, SecondSeat, first.UserId));
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
    /// <summary>
    /// 这个用户是否是本房间的**玩家**(黑方或白方)。
    /// <para>
    /// 加它是为了一件安全判定:围观频道的消息只能给围观者看,而"谁不是玩家"是那条规则的判据。
    /// 此前没有这个谓词,于是判定散在各处 —— 而实际情况是**三条读取路径全都没做这个判定**,
    /// 围观频道的保密性完全依赖客户端自觉。
    /// </para>
    /// </summary>
    /// <param name="userId">要判断的用户。</param>
    public bool IsPlayer(UserId userId)
        => userId == BlackPlayerId || (WhitePlayerId is not null && userId == WhitePlayerId.Value);

    /// <summary>这个用户是否在围观者集合里。</summary>
    /// <param name="userId">要判断的用户。</param>
    public bool IsSpectator(UserId userId) => _spectators.Any(s => s.UserId == userId);

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
    /// 走子领域入口。顺序:房间态校验 → 身份校验 → 回合校验 → <c>rules.Apply</c> →
    /// 记录 <see cref="SubMove"/> → 翻转回合 → 可能转入 Finished。
    /// <para>
    /// **聚合根只管前三条。** 越界、重复落子、走法合不合规,全部由 <c>rules.Apply</c> 回答 ——
    /// 盘面语义整个属于规则。这是象棋能进这个聚合的前提:它一格上是七种棋子之一 × 两方,
    /// 胜负是将死 / 困毙,与最后一步的位置没有直接关系。
    /// </para>
    /// </summary>
    /// <param name="userId">走子的玩家。</param>
    /// <param name="intent">这一步想怎么走;落子类棋种的 <c>From</c> 为 <c>null</c>。</param>
    /// <param name="now">走子时刻(UTC)。</param>
    /// <param name="rules">本房间棋种的规则。</param>
    /// <summary>
    /// 这个用户坐在几号座位?不是玩家则 <c>null</c>。
    /// <para>
    /// 三处需要"这人是第几号"的地方(落子、催促、以后的出牌)此前各写了一遍同样的
    /// if/else if/else。合成一处,是因为那三份里任何一份漏掉一个座位,表现都是
    /// "某个座位的人被当成不是玩家" —— 而座位变多之后,漏的概率随座位数涨。
    /// </para>
    /// </summary>
    /// <param name="userId">用户。</param>
    public int? SeatOf(UserId userId)
    {
        return _seats.SingleOrDefault(x => x.UserId == userId)?.Index;
    }

    /// <summary>
    /// 只对两座位棋种有定义的路径的前置条件 —— 认输与超时判负都要指出**一个**赢家。
    /// <para>
    /// 座位数从 <c>_seats.Count</c> 读,不需要 <c>IGameRules</c>:这两条路径只在 <c>Playing</c>
    /// 状态下走到这里,而房间是坐满才开局的,所以 <c>_seats.Count == rules.SeatCount</c>。
    /// 为一个已知的事实多要一个参数,是让每个调用方都替这里查一遍。
    /// </para>
    /// </summary>
    /// <param name="what">在做什么,用于错误消息。</param>
    /// <exception cref="SeatCountNotSupportedException">座位数不是 2。</exception>
    private void RequireTwoSeats(string what)
    {
        if (_seats.Count != 2)
        {
            throw new SeatCountNotSupportedException(
                $"{what} needs exactly two seats to name a single winner; this room has {_seats.Count}.");
        }
    }

    /// <summary>两座位棋种里"另一个座位"。仅在 <see cref="RequireTwoSeats"/> 通过之后调用。</summary>
    /// <param name="seat">这一个座位。</param>
    private static int OtherSeat(int seat) => seat == FirstSeat ? 1 : FirstSeat;

    public MoveOutcome PlayMove(UserId userId, MoveIntent intent, DateTime now, IGameRules rules)
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

        var seat = SeatOf(userId)
            ?? throw new NotAPlayerException($"User {userId.Value} is not a player in this room.");

        if (seat != Game.CurrentTurn)
        {
            throw new NotYourTurnException(
                $"It is not seat {seat}'s turn; current turn is seat {Game.CurrentTurn}.");
        }

        return ApplyMove(seat, intent, now, rules);
    }

    /// <summary>
    /// 把一步棋交给规则、记录下来、必要时结束对局 —— <see cref="PlayMove"/> 与超时兜底**共用**这一条。
    /// <para>
    /// 抽出来是必须的,不是整洁:兜底那一步也可能**结束对局**(牌类里替人出掉最后一手牌,
    /// 那一手就赢了),而两条路径各写一遍会让「<c>Apply</c> 是走子合法性与胜负判定的**唯一**入口」
    /// 变成两个入口。
    /// </para>
    /// <para>
    /// 它**不**验"这人是不是玩家、是不是他的回合" —— 那两条是 <see cref="PlayMove"/> 独有的:
    /// 超时兜底的座位由 <c>CurrentTurn</c> 给出,没有一个"调用者"需要被核对身份。
    /// </para>
    /// </summary>
    /// <param name="seat">走这一步的座位号。</param>
    /// <param name="intent">这一步怎么走。</param>
    /// <param name="now">当前时间(UTC)。</param>
    /// <param name="rules">本房间棋种的规则。</param>
    /// <exception cref="Exceptions.InvalidMoveException">规则判这一步非法。</exception>
    private MoveOutcome ApplyMove(int seat, MoveIntent intent, DateTime now, IGameRules rules)
    {
        // 盘面语义整个属于规则:越界、重复落子、走法合不合规,全部由 Apply 回答。
        // 非法则抛 InvalidMoveException 向上冒泡(对外仍是 409),而 Game 的 Moves
        // 在此之前尚未追加,聚合状态不变。
        var application = rules.Apply(Game!.State(), intent, seat);
        var result = application.Result;

        var appended = Game.RecordMove(
            intent, seat, rules.SeatCount, application.NextSeat, now);

        if (result != GameResult.Ongoing)
        {
            // 赢家从**座位**查,而不是从结果值 switch 出黑方 / 白方。此前那个 switch 要求
            // 结果枚举自己带着颜色 —— 而"谁赢了"同一个事实已经写在 WinnerUserId 里了,
            // 两份真源。顺带:座位号没有上限,`Decided` 也就能表示 2 号座位赢。
            var winnerId = application.WinnerSeat is int winnerSeat
                ? PlayerAt(winnerSeat)
                : null;
            Game.FinishWith(result, winnerId, GameEndReason.Decided, now);
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

        var seat = SeatOf(userId)
            ?? throw new NotAPlayerException(
                $"User {userId.Value} is not a player in this room and cannot resign.");

        RequireTwoSeats("Resigning");

        var opponentUserId = PlayerAt(OtherSeat(seat))!.Value;

        Game.FinishWith(GameResult.Decided, opponentUserId, GameEndReason.Resigned, now);
        TransitionStatus(RoomStatus.Finished);
        return new GameEndOutcome(GameResult.Decided, opponentUserId);
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
    /// <param name="now">当前时间(UTC)。</param>
    /// <param name="turnTimeoutSeconds">超时阈值(秒),至少 1。</param>
    /// <param name="rules">
    /// 本房间棋种的规则。实现 <c>ITimeoutFallbackRules</c> 时,超时**替他走一步**而不是判他负。
    /// </param>
    public TurnTimeoutOutcome TimeOutCurrentTurn(
        DateTime now, int turnTimeoutSeconds, IGameRules rules)
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

        // 有兜底的棋种:替他走一步,对局继续。那一步走**与真人落子完全相同的路径** ——
        // 它也可能非法(实现出错),更要紧的是它可能结束对局(替人出掉最后一手牌,那一手就赢了)。
        if (rules is ITimeoutFallbackRules fallback)
        {
            var seat = Game.CurrentTurn;
            var intent = fallback.MoveOnTimeout(Game.History(), seat);
            return TurnTimeoutOutcome.Played(ApplyMove(seat, intent, now, rules));
        }

        // 没有兜底:判他负、对手胜 —— 而"对手"只在两个座位时唯一。这条限制没有被放宽,
        // 只是上面那条路给了它一个正当的出口:一个三座位棋种若不提供兜底,仍然在这里大声坏掉。
        RequireTwoSeats("Timing a turn out");

        var winnerUserId = PlayerAt(OtherSeat(Game.CurrentTurn))!.Value;

        Game.FinishWith(GameResult.Decided, winnerUserId, GameEndReason.TurnTimeout, now);
        TransitionStatus(RoomStatus.Finished);
        return TurnTimeoutOutcome.Finished(new GameEndOutcome(GameResult.Decided, winnerUserId));
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

        var senderSeat = SeatOf(senderId)
            ?? throw new NotAPlayerException(
                $"User {senderId.Value} is not a player and cannot urge.");
        var urgedUser = senderSeat == FirstSeat ? WhitePlayerId!.Value : BlackPlayerId;

        if (senderSeat == Game.CurrentTurn)
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
