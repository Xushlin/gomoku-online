namespace Gomoku.Domain.Exceptions;

// 这个文件集中承载 Room 聚合与其子实体的领域级异常。每个异常都是 sealed,
// 都继承 System.Exception,消息由调用方传入(指明触发的具体上下文)。
// 聚合到一个文件是为了便于 code review 和跨引用 —— Room/Game/ChatMessage
// 的所有不变量守卫都在此一览。若未来某类异常扩展为携带额外数据(例如
// InvalidRoomNameException 要带"违反的规则名"),再单独拆成独立文件。

/// <summary>房间名非法(空白 / 长度不在 3–50)。Api 层映射 HTTP 400。</summary>
public sealed class InvalidRoomNameException : Exception
{
    /// <inheritdoc />
    public InvalidRoomNameException(string message) : base(message) { }
}

/// <summary>尝试了非法的房间状态转换(Waiting → Playing → Finished 以外的路径)。Api 层映射 400。</summary>
public sealed class InvalidRoomStatusTransitionException : Exception
{
    /// <inheritdoc />
    public InvalidRoomStatusTransitionException(string message) : base(message) { }
}

/// <summary>操作要求房间处于 Waiting(例如加入为玩家),但当前不是。Api 层映射 409。</summary>
public sealed class RoomNotWaitingException : Exception
{
    /// <inheritdoc />
    public RoomNotWaitingException(string message) : base(message) { }
}

/// <summary>操作要求房间处于 Playing(例如落子 / 催促),但当前不是。Api 层映射 409。</summary>
public sealed class RoomNotInPlayException : Exception
{
    /// <inheritdoc />
    public RoomNotInPlayException(string message) : base(message) { }
}

/// <summary>房间两个玩家位已被占满,无法再加入为玩家。Api 层映射 409。</summary>
public sealed class RoomFullException : Exception
{
    /// <inheritdoc />
    public RoomFullException(string message) : base(message) { }
}

/// <summary>用户已在房间内(玩家或围观者),不可重复加入同一角色。Api 层映射 409。</summary>
public sealed class AlreadyInRoomException : Exception
{
    /// <inheritdoc />
    public AlreadyInRoomException(string message) : base(message) { }
}

/// <summary>Waiting 状态下 Host 尝试"离开"自己的房间 —— 请用"解散房间"接口(本变更不含)。Api 层映射 409。</summary>
public sealed class HostCannotLeaveWaitingRoomException : Exception
{
    /// <inheritdoc />
    public HostCannotLeaveWaitingRoomException(string message) : base(message) { }
}

/// <summary>玩家不能作为围观者加入自己的对局。Api 层映射 409。</summary>
public sealed class PlayerCannotSpectateException : Exception
{
    /// <inheritdoc />
    public PlayerCannotSpectateException(string message) : base(message) { }
}

/// <summary>操作要求用户在房间内(玩家或围观者),但他不在。Api 层映射 404。</summary>
public sealed class NotInRoomException : Exception
{
    /// <inheritdoc />
    public NotInRoomException(string message) : base(message) { }
}

/// <summary>用户尝试离开围观,但他并不在围观者集合中。Api 层映射 404。</summary>
public sealed class NotSpectatingException : Exception
{
    /// <inheritdoc />
    public NotSpectatingException(string message) : base(message) { }
}

/// <summary>非玩家(围观者或无关用户)尝试执行玩家才能做的事(落子 / 催促)。Api 层映射 403。</summary>
public sealed class NotAPlayerException : Exception
{
    /// <inheritdoc />
    public NotAPlayerException(string message) : base(message) { }
}

/// <summary>落子时不是你的回合。Api 层映射 409。</summary>
public sealed class NotYourTurnException : Exception
{
    /// <inheritdoc />
    public NotYourTurnException(string message) : base(message) { }
}

/// <summary>催促对手时发现当前正是自己的回合 —— 催自己毫无意义。Api 层映射 409。</summary>
public sealed class NotOpponentsTurnException : Exception
{
    /// <inheritdoc />
    public NotOpponentsTurnException(string message) : base(message) { }
}

/// <summary>催促过于频繁(冷却期内再催)。Api 层映射 HTTP 429。</summary>
public sealed class UrgeTooFrequentException : Exception
{
    /// <inheritdoc />
    public UrgeTooFrequentException(string message) : base(message) { }
}

/// <summary>聊天内容非法(空白 / 超长)。Api 层映射 400。</summary>
public sealed class InvalidChatContentException : Exception
{
    /// <inheritdoc />
    public InvalidChatContentException(string message) : base(message) { }
}

/// <summary>玩家尝试在"围观者频道"发消息 —— 玩家只能发房间频道。Api 层映射 403。</summary>
public sealed class PlayerCannotPostSpectatorChannelException : Exception
{
    /// <inheritdoc />
    public PlayerCannotPostSpectatorChannelException(string message) : base(message) { }
}
