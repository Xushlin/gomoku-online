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
/// <param name="IncludeSpectatorChat">这份快照是否包含围观频道的消息。</param>
public readonly record struct RoomView(bool IncludeSpectatorChat)
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
    public static RoomView For(Room room, UserId viewer) => new(room.IsSpectator(viewer));

    /// <summary>广播给"非围观者"子群的那一份 —— 不含围观频道。</summary>
    public static RoomView ForNonSpectators => new(false);

    /// <summary>广播给围观者子群的那一份 —— 含围观频道。</summary>
    public static RoomView ForSpectators => new(true);
}
