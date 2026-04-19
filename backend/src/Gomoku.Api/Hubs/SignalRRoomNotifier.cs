using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using Microsoft.AspNetCore.SignalR;

namespace Gomoku.Api.Hubs;

/// <summary>
/// <see cref="IRoomNotifier"/> 的 SignalR 实现。按 design D7 / D15 的 group 命名规则推送,
/// 围观频道消息只发给 spectators 子群,催促事件只发给被催的那一方用户。
/// </summary>
public sealed class SignalRRoomNotifier : IRoomNotifier
{
    private readonly IHubContext<GomokuHub> _hub;

    /// <inheritdoc />
    public SignalRRoomNotifier(IHubContext<GomokuHub> hub)
    {
        _hub = hub;
    }

    /// <inheritdoc />
    public Task RoomStateChangedAsync(RoomId roomId, RoomStateDto state, CancellationToken ct) =>
        _hub.Clients.Group(GomokuHub.RoomGroupName(roomId)).SendAsync("RoomState", state, ct);

    /// <inheritdoc />
    public Task PlayerJoinedAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(GomokuHub.RoomGroupName(roomId)).SendAsync("PlayerJoined", user, ct);

    /// <inheritdoc />
    public Task PlayerLeftAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(GomokuHub.RoomGroupName(roomId)).SendAsync("PlayerLeft", user, ct);

    /// <inheritdoc />
    public Task SpectatorJoinedAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(GomokuHub.RoomGroupName(roomId)).SendAsync("SpectatorJoined", user, ct);

    /// <inheritdoc />
    public Task SpectatorLeftAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(GomokuHub.RoomGroupName(roomId)).SendAsync("SpectatorLeft", user, ct);

    /// <inheritdoc />
    public Task MoveMadeAsync(RoomId roomId, MoveDto move, CancellationToken ct) =>
        _hub.Clients.Group(GomokuHub.RoomGroupName(roomId)).SendAsync("MoveMade", move, ct);

    /// <inheritdoc />
    public Task GameEndedAsync(RoomId roomId, GameEndedDto payload, CancellationToken ct) =>
        _hub.Clients.Group(GomokuHub.RoomGroupName(roomId)).SendAsync("GameEnded", payload, ct);

    /// <inheritdoc />
    public Task ChatMessagePostedAsync(RoomId roomId, ChatChannel channel, ChatMessageDto message, CancellationToken ct)
    {
        var group = channel == ChatChannel.Spectator
            ? GomokuHub.SpectatorsGroupName(roomId)
            : GomokuHub.RoomGroupName(roomId);
        return _hub.Clients.Group(group).SendAsync("ChatMessage", message, ct);
    }

    /// <inheritdoc />
    public Task OpponentUrgedAsync(RoomId roomId, UserId urgedUser, UrgeDto payload, CancellationToken ct) =>
        _hub.Clients.User(urgedUser.Value.ToString()).SendAsync("UrgeReceived", payload, ct);
}
