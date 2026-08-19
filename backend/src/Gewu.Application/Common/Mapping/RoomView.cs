using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;

namespace Gewu.Application.Common.Mapping;

/// <summary>
/// 一份房间快照**给谁看** —— <c>ToState</c> 的必需参数。
/// <para>
/// 它存在的理由是一条安全规则:`in-room-chat` 规定围观频道**仅围观者可见**。那条规则的写入侧
/// 一直是强制的(玩家发围观频道抛 <c>PlayerCannotPostSpectatorChannelException</c>),而读取侧
/// **三条路全都没做判定**,围观频道的保密性完全依赖客户端自觉。
/// </para>
/// <para>
/// 为什么是必需参数而不是可选:一个默认值会让「忘了表态」与「故意给全部」在代码里长得一模一样,
/// 而这正是那个缺陷能存在这么久的形状。必需参数把它变成编译器的问题 ——
/// 这个仓库反复用的那句:**一张需要记得扩充的表是纪律,一个构造函数参数是编译器。**
/// </para>
/// </summary>
/// <para>
/// <b>它现在有两个维度。</b> 第二个是**这份快照给哪个座位看**:斗地主的手牌只有一个座位能看,
/// 而"围观频道给不给"答不了这个问题。两个维度都在这里,是因为它们回答的是同一个问题的两半 ——
/// "这份快照是给谁的",而把它们分成两个参数就等于给每个调用点一次只想起一半的机会。
/// </para>
/// <param name="IncludeSpectatorChat">这份快照是否包含围观频道的消息。</param>
/// <param name="Seat">看这份快照的人坐第几号座位;不占座位(围观者 / 尚未入座)时 <c>null</c>。</param>
/// <param name="SeatView">
/// 这个座位**能看到**的那一份棋种私有状态,已由规则序列化;棋种没有隐藏信息时 <c>null</c>。
/// <para>
/// 对内核完全不透明 —— 它原样进 <c>GameSnapshotDto.SeatView</c>。内核 MUST NOT 解析它。
/// </para>
/// </param>
public readonly record struct RoomView(bool IncludeSpectatorChat, int? Seat, string? SeatView)
{
    /// <summary>
    /// 给某个具体的人看。**只有围观者**看得到围观频道。
    /// <para>
    /// 判据是"是不是围观者",而**不是**"不是玩家"。两者对一个还没点「围观」的旁观者给出不同
    /// 答案,而我第一版写的正是后者 —— 它让 REST 与广播不一致:REST 会把围观频道给他,而广播
    /// 分组里他既不在 players 也不在 spectators,于是干脆收不到实时更新。
    /// </para>
    /// <para>
    /// 取「是围观者」这一侧,是因为它同时更保守、更一致、也更符合直觉:围观区是围观者的地盘。
    /// 代价是"先看看再决定围观"看不到评论,而那一步只要点一下大厅的「围观」按钮就跨过去了 ——
    /// 正常路径本来就是先 <c>POST /spectate</c> 再进房。
    /// </para>
    /// </summary>
    /// <param name="room">房间。</param>
    /// <param name="viewer">看这份快照的人。</param>
    /// <param name="rules">这个房间的棋种规则 —— 用来算出这个座位的私有切片。</param>
    public static RoomView For(Room room, UserId viewer, IGameRules? rules)
        => Build(room, room.IsSpectator(viewer), room.SeatOf(viewer), rules);

    /// <summary>广播给某一个座位的那一份 —— 含那个座位的私有状态,不含围观频道。</summary>
    /// <param name="room">房间。</param>
    /// <param name="seat">座位号。</param>
    /// <param name="rules">棋种规则。</param>
    public static RoomView ForSeat(Room room, int seat, IGameRules? rules)
        => Build(room, includeSpectatorChat: false, seat: seat, rules: rules);

    /// <summary>
    /// 广播给"在房间里但没坐座位、也没围观"的那一份 —— 既没有围观频道,也没有任何私有状态。
    /// </summary>
    /// <param name="room">房间。</param>
    /// <param name="rules">棋种规则。</param>
    public static RoomView ForObservers(Room room, IGameRules? rules)
        => Build(room, includeSpectatorChat: false, seat: null, rules: rules);

    /// <summary>广播给围观者子群的那一份 —— 含围观频道,不含任何座位的私有状态。</summary>
    /// <param name="room">房间。</param>
    /// <param name="rules">棋种规则。</param>
    public static RoomView ForSpectators(Room room, IGameRules? rules)
        => Build(room, includeSpectatorChat: true, seat: null, rules: rules);

    private static RoomView Build(Room room, bool includeSpectatorChat, int? seat, IGameRules? rules)
    {
        // 私有切片只在**对局已经开始**时存在:规则要从 `MatchState` 重建局面,而 Waiting 房间
        // 还没有 Game,也没有发牌。这不是防御性判空 —— 大厅列表与等待中的房间都会走到这里。
        var seatView = rules is IPerSeatViewRules perSeat && room.Game is not null
            ? perSeat.ViewFor(room.Game.State(), seat)
            : null;
        return new RoomView(includeSpectatorChat, seat, seatView);
    }
}
