using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Mapping;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Microsoft.AspNetCore.SignalR;

namespace Gewu.Api.Hubs;

/// <summary>
/// <see cref="IRoomNotifier"/> 的 SignalR 实现。按 design D7 / D15 的 group 命名规则推送,
/// 围观频道消息只发给 spectators 子群,催促事件只发给被催的那一方用户。
/// </summary>
public sealed class SignalRRoomNotifier : IRoomNotifier
{
    private readonly IHubContext<MatchHub> _hub;

    /// <inheritdoc />
    public SignalRRoomNotifier(IHubContext<MatchHub> hub)
    {
        _hub = hub;
    }

    /// <summary>
    /// 推完整房间状态 —— **分两份**。
    /// <para>
    /// 给玩家子群的那份不含围观频道,给围观者子群的那份含。此前这里是一份 DTO 推给
    /// <c>room:{id}</c>(全体),于是围观者的吐槽进了玩家的客户端。
    /// </para>
    /// <para>
    /// 两个目标群 MUST **互斥且穷尽**:<c>JoinRoom</c> 按聚合身份把每个连接放进
    /// spectators 或 non-spectators 之一,所以每个连接恰好收到一份。互斥不成立会让某个连接
    /// 收到两份、由到达顺序决定它看到什么;不穷尽会让某个连接一份都收不到 ——
    /// 后者是我第一版按"玩家"分组时真的发生的事。
    /// </para>
    /// </summary>
    /// <param name="room">房间聚合。</param>
    /// <param name="usernames">用户名字典。</param>
    /// <param name="turnTimeoutSeconds">回合超时秒数。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task RoomStateChangedAsync(
        Room room,
        IReadOnlyDictionary<Guid, string> usernames,
        int turnTimeoutSeconds,
        CancellationToken ct)
    {
        var forNonSpectators = room.ToState(usernames, turnTimeoutSeconds, RoomView.ForNonSpectators);
        var forSpectators = room.ToState(usernames, turnTimeoutSeconds, RoomView.ForSpectators);

        await _hub.Clients.Group(MatchHub.NonSpectatorsGroupName(room.Id))
            .SendAsync("RoomState", forNonSpectators, ct);
        await _hub.Clients.Group(MatchHub.SpectatorsGroupName(room.Id))
            .SendAsync("RoomState", forSpectators, ct);
    }

    /// <inheritdoc />
    public Task PlayerJoinedAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("PlayerJoined", user, ct);

    /// <inheritdoc />
    public Task PlayerLeftAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("PlayerLeft", user, ct);

    /// <inheritdoc />
    public Task SpectatorJoinedAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("SpectatorJoined", user, ct);

    /// <inheritdoc />
    public Task SpectatorLeftAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("SpectatorLeft", user, ct);

    /// <inheritdoc />
    public Task MoveMadeAsync(RoomId roomId, MoveDto move, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("MoveMade", move, ct);

    /// <inheritdoc />
    public Task GameEndedAsync(RoomId roomId, GameEndedDto payload, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("GameEnded", payload, ct);

    /// <inheritdoc />
    public Task ChatMessagePostedAsync(RoomId roomId, ChatChannel channel, ChatMessageDto message, CancellationToken ct)
    {
        var group = channel == ChatChannel.Spectator
            ? MatchHub.SpectatorsGroupName(roomId)
            : MatchHub.RoomGroupName(roomId);
        return _hub.Clients.Group(group).SendAsync("ChatMessage", message, ct);
    }

    /// <inheritdoc />
    public Task OpponentUrgedAsync(RoomId roomId, UserId urgedUser, UrgeDto payload, CancellationToken ct) =>
        _hub.Clients.User(urgedUser.Value.ToString()).SendAsync("UrgeReceived", payload, ct);

    /// <inheritdoc />
    public Task RoomDissolvedAsync(RoomId roomId, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId))
            .SendAsync("RoomDissolved", new { RoomId = roomId.Value }, ct);
}
