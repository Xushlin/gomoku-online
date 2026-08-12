namespace Gomoku.Domain.Rooms;

/// <summary>
/// 房间生命周期状态。
/// 合法转换仅:<see cref="Waiting"/> → <see cref="Playing"/> → <see cref="Finished"/>(单向)。
/// 违反此约束应抛 <see cref="Exceptions.InvalidRoomStatusTransitionException"/>。
/// </summary>
public enum RoomStatus
{
    /// <summary>等待第二位玩家加入。</summary>
    Waiting = 0,

    /// <summary>两位玩家就位,对局进行中。</summary>
    Playing = 1,

    /// <summary>对局已结束(某方胜或平局)。</summary>
    Finished = 2,
}
