namespace Gewu.Domain.Exceptions;

// 这个文件集中承载 Room 聚合与其子实体的领域级异常。每个异常都是 sealed,
// 都继承 System.Exception,消息由调用方传入(指明触发的具体上下文)。
// 聚合到一个文件是为了便于 code review 和跨引用 —— Room/Game/ChatMessage
// 的所有不变量守卫都在此一览。若未来某类异常扩展为携带额外数据(例如
// InvalidRoomNameException 要带"违反的规则名"),再单独拆成独立文件。

/// <summary>房间名非法(空白 / 长度不在 3–50)。Api 层映射 HTTP 400。</summary>
public sealed class InvalidRoomNameException : DomainException
{
    /// <inheritdoc />
    public InvalidRoomNameException(string message) : base("invalid-room-name", message) { }
}

/// <summary>尝试了非法的房间状态转换(Waiting → Playing → Finished 以外的路径)。Api 层映射 400。</summary>
public sealed class InvalidRoomStatusTransitionException : DomainException
{
    /// <inheritdoc />
    public InvalidRoomStatusTransitionException(string message) : base("invalid-room-status-transition", message) { }
}

/// <summary>操作要求房间处于 Waiting(例如加入为玩家),但当前不是。Api 层映射 409。</summary>
public sealed class RoomNotWaitingException : DomainException
{
    /// <inheritdoc />
    public RoomNotWaitingException(string message) : base("room-not-waiting", message) { }
}

/// <summary>操作要求房间处于 Playing(例如落子 / 催促),但当前不是。Api 层映射 409。</summary>
public sealed class RoomNotInPlayException : DomainException
{
    /// <inheritdoc />
    public RoomNotInPlayException(string message) : base("room-not-in-play", message) { }
}

/// <summary>房间两个玩家位已被占满,无法再加入为玩家。Api 层映射 409。</summary>
public sealed class RoomFullException : DomainException
{
    /// <inheritdoc />
    public RoomFullException(string message) : base("room-full", message) { }
}

/// <summary>
/// 这条路径只对两座位棋种有定义,而本房间的座位数不是 2。Api 层映射 409。
/// <para>
/// "认输"与"超时判负"都要指出**一个**赢家,而"对手"只在两个座位时唯一。三个座位时它有两个,
/// 谁赢是那个棋种的规则问题(斗地主里取决于超时的是地主还是农民),不是这里可以默认的。
/// </para>
/// <para>
/// **拒绝而不是猜。** 旧代码在三座位下的行为是:0 号认输判 1 号胜,2 号完全不在话下,而 2 号
/// 认输会得到 <c>NotAPlayerException</c> —— 三个静默的错答案。今天没有三座位棋种,所以这条
/// 抛不出来;它存在是为了让第一个三座位棋种**必须**先回答这个问题,而不是继承一个错的默认。
/// </para>
/// </summary>
public sealed class SeatCountNotSupportedException : DomainException
{
    /// <inheritdoc />
    public SeatCountNotSupportedException(string message) : base("seat-count-not-supported", message) { }
}

/// <summary>用户已在房间内(玩家或围观者),不可重复加入同一角色。Api 层映射 409。</summary>
public sealed class AlreadyInRoomException : DomainException
{
    /// <inheritdoc />
    public AlreadyInRoomException(string message) : base("already-in-room", message) { }
}

/// <summary>Waiting 状态下 Host 尝试"离开"自己的房间 —— 请用"解散房间"接口(本变更不含)。Api 层映射 409。</summary>
public sealed class HostCannotLeaveWaitingRoomException : DomainException
{
    /// <inheritdoc />
    public HostCannotLeaveWaitingRoomException(string message) : base("host-cannot-leave-waiting-room", message) { }
}

/// <summary>玩家不能作为围观者加入自己的对局。Api 层映射 409。</summary>
public sealed class PlayerCannotSpectateException : DomainException
{
    /// <inheritdoc />
    public PlayerCannotSpectateException(string message) : base("player-cannot-spectate", message) { }
}

/// <summary>操作要求用户在房间内(玩家或围观者),但他不在。Api 层映射 404。</summary>
public sealed class NotInRoomException : DomainException
{
    /// <inheritdoc />
    public NotInRoomException(string message) : base("not-in-room", message) { }
}

/// <summary>用户尝试离开围观,但他并不在围观者集合中。Api 层映射 404。</summary>
public sealed class NotSpectatingException : DomainException
{
    /// <inheritdoc />
    public NotSpectatingException(string message) : base("not-spectating", message) { }
}

/// <summary>非玩家(围观者或无关用户)尝试执行玩家才能做的事(落子 / 催促)。Api 层映射 403。</summary>
public sealed class NotAPlayerException : DomainException
{
    /// <inheritdoc />
    public NotAPlayerException(string message) : base("not-a-player", message) { }
}

/// <summary>落子时不是你的回合。Api 层映射 409。</summary>
public sealed class NotYourTurnException : DomainException
{
    /// <inheritdoc />
    public NotYourTurnException(string message) : base("not-your-turn", message) { }
}

/// <summary>催促对手时发现当前正是自己的回合 —— 催自己毫无意义。Api 层映射 409。</summary>
public sealed class NotOpponentsTurnException : DomainException
{
    /// <inheritdoc />
    public NotOpponentsTurnException(string message) : base("not-opponents-turn", message) { }
}

/// <summary>催促过于频繁(冷却期内再催)。Api 层映射 HTTP 429。</summary>
public sealed class UrgeTooFrequentException : DomainException
{
    /// <inheritdoc />
    public UrgeTooFrequentException(string message) : base("urge-too-frequent", message) { }
}

/// <summary>聊天内容非法(空白 / 超长)。Api 层映射 400。</summary>
public sealed class InvalidChatContentException : DomainException
{
    /// <inheritdoc />
    public InvalidChatContentException(string message) : base("invalid-chat-content", message) { }
}

/// <summary>玩家尝试在"围观者频道"发消息 —— 玩家只能发房间频道。Api 层映射 403。</summary>
public sealed class PlayerCannotPostSpectatorChannelException : DomainException
{
    /// <inheritdoc />
    public PlayerCannotPostSpectatorChannelException(string message) : base("spectator-channel-forbidden", message) { }
}

/// <summary>
/// 操作要求调用方是 Host 但不是(例如非 Host 用户尝试 <c>DELETE /api/rooms/{id}</c> 解散)。
/// 与 <see cref="NotAPlayerException"/> 区分:后者是"甚至都不是玩家",本异常是"是玩家 / 围观者但不是 Host"。
/// Api 层映射 HTTP 403。
/// </summary>
public sealed class NotRoomHostException : DomainException
{
    /// <inheritdoc />
    public NotRoomHostException(string message) : base("not-room-host", message) { }
}

/// <summary>
/// <c>Room.TimeOutCurrentTurn</c> 被过早调用(当前回合未超过 <c>turnTimeoutSeconds</c>)。
/// 典型出现在 <c>TurnTimeoutWorker</c> 查询与 handler 执行之间,对手刚落了一子让 lastActivity 推新;
/// worker 应 try/catch 吞下此异常 —— 下轮查询不会再包含该房间。Api 层映射 HTTP 409。
/// </summary>
public sealed class TurnNotTimedOutException : DomainException
{
    /// <inheritdoc />
    public TurnNotTimedOutException(string message) : base("turn-not-timed-out", message) { }
}
