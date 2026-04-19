using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;

namespace Gomoku.Api.Hubs;

/// <summary>
/// 跟踪当前所有 SignalR 连接与其关联的 UserId / RoomId 集合。
/// 当前实现为**单进程内存**;未来水平扩展时换 Redis 支持的实现即可,无需改 Hub 代码。
/// </summary>
public interface IConnectionTracker
{
    /// <summary>连接建立时绑定 UserId。</summary>
    ValueTask TrackAsync(string connectionId, UserId userId);

    /// <summary>连接断开时清理其所有关联。</summary>
    ValueTask UntrackAsync(string connectionId);

    /// <summary>把连接加入某房间的"跟踪记录"(仅供内部回收,不等价于 SignalR group)。</summary>
    ValueTask AssociateRoomAsync(string connectionId, RoomId roomId);

    /// <summary>从房间的"跟踪记录"里移除该连接。</summary>
    ValueTask DissociateRoomAsync(string connectionId, RoomId roomId);
}
