using Gewu.Application.Common.DTOs;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;

namespace Gewu.Application.Abstractions;

/// <summary>
/// 房间相关事件的广播契约。Handler 在 <c>SaveChangesAsync</c> **之后** 调用本接口,
/// 让 Api 层(SignalR 实现)把事件推送到对应 group。
/// <para>
/// Application 层通过这个抽象与 SignalR 解耦;未来要换成 Kafka / WebSocket 自写协议 /
/// SSE 都只需换实现,Handler 不动。
/// </para>
/// </summary>
public interface IRoomNotifier
{
    /// <summary>
    /// 推完整房间状态(用于对齐,优先于细粒度事件)。
    /// <para>
    /// <b>收的是原料,不是成品 DTO</b> —— 因为这一次广播要发**两份**:给玩家的那份不含围观频道,
    /// 给围观者的那份含。让调用方各自投影两次,就等于让每个 handler 都有一次忘掉裁剪的机会,
    /// 而那正是这个缺陷此前的形状(一份 DTO 推给整个房间 group)。
    /// </para>
    /// <para>
    /// 实现方 MUST 分群推送,MUST NOT 把含围观频道的那份推给玩家。
    /// </para>
    /// </summary>
    /// <param name="room">房间聚合 —— 实现方据此投影两份视图。</param>
    /// <param name="usernames">用户名字典。</param>
    /// <param name="turnTimeoutSeconds">回合超时秒数,嵌进快照供客户端倒计时。</param>
    /// <param name="ct">取消令牌。</param>
    Task RoomStateChangedAsync(
        Room room,
        IReadOnlyDictionary<Guid, string> usernames,
        int turnTimeoutSeconds,
        CancellationToken ct);

    /// <summary>玩家加入房间。</summary>
    Task PlayerJoinedAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct);

    /// <summary>玩家离开房间。</summary>
    Task PlayerLeftAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct);

    /// <summary>围观者加入房间。</summary>
    Task SpectatorJoinedAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct);

    /// <summary>围观者离开房间(与加入对称广播)。</summary>
    Task SpectatorLeftAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct);

    /// <summary>一步棋已落下。</summary>
    Task MoveMadeAsync(RoomId roomId, MoveDto move, CancellationToken ct);

    /// <summary>对局结束(胜 / 平)。</summary>
    Task GameEndedAsync(RoomId roomId, GameEndedDto payload, CancellationToken ct);

    /// <summary>聊天消息已发表(房间频道广播到所有人,围观频道仅围观者)。</summary>
    Task ChatMessagePostedAsync(RoomId roomId, ChatChannel channel, ChatMessageDto message, CancellationToken ct);

    /// <summary>对手被催了(仅推给 <paramref name="urgedUser"/> 本人)。</summary>
    Task OpponentUrgedAsync(RoomId roomId, UserId urgedUser, UrgeDto payload, CancellationToken ct);

    /// <summary>
    /// 房间已被 Host 解散并物理删除。向 <c>room:{roomId}</c> group 广播,payload 仅含 roomId;
    /// 前端据此从列表 / 围观视图移除该房间。Handler MUST 在 <c>SaveChangesAsync</c> 后调。
    /// </summary>
    Task RoomDissolvedAsync(RoomId roomId, CancellationToken ct);
}
