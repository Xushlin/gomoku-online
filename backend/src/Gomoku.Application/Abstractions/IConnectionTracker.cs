using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;

namespace Gomoku.Application.Abstractions;

/// <summary>
/// 跨 SignalR 连接的用户在线状态追踪契约。由 Api 层(<c>Gomoku.Api.Hubs.ConnectionTracker</c>)
/// 实现为内存引用计数;未来水平扩展改 Redis 实现即可,Application / Domain 不动。
/// <para>
/// 单用户多连接(多标签 / 多设备)算**一个在线**:引用计数 ≥ 1 即 online;最后一条连接
/// Untrack 后 counts 归零并从字典移除。
/// </para>
/// </summary>
public interface IConnectionTracker
{
    /// <summary>连接建立时绑定 UserId 并递增其引用计数。</summary>
    ValueTask TrackAsync(string connectionId, UserId userId);

    /// <summary>连接断开时清理其所有关联并递减引用计数;归零时从字典移除。</summary>
    ValueTask UntrackAsync(string connectionId);

    /// <summary>把连接加入某房间的"跟踪记录"(仅供内部回收,不等价于 SignalR group)。</summary>
    ValueTask AssociateRoomAsync(string connectionId, RoomId roomId);

    /// <summary>从房间的"跟踪记录"里移除该连接。</summary>
    ValueTask DissociateRoomAsync(string connectionId, RoomId roomId);

    /// <summary>当前至少有一条活连接的不同 <see cref="UserId"/> 数(去重)。</summary>
    int GetOnlineUserCount();

    /// <summary>指定用户当前是否至少有一条活连接。未知 UserId 返回 false(不抛异常)。</summary>
    bool IsUserOnline(UserId userId);
}
