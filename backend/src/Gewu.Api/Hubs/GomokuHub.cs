using System.IdentityModel.Tokens.Jwt;
using Gomoku.Application.Abstractions;
using Gomoku.Application.Features.Rooms.MakeMove;
using Gomoku.Application.Features.Rooms.SendChatMessage;
using Gomoku.Application.Features.Rooms.UrgeOpponent;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Serilog.Context;

namespace Gomoku.Api.Hubs;

/// <summary>
/// 单一 SignalR Hub:所有实时操作都经它路由到 MediatR handler。Hub 本身 MUST NOT
/// 读写数据库或直接发送业务事件 —— 事件由 handler 完成 <c>SaveChangesAsync</c> 后通过
/// <c>IRoomNotifier</c> 广播。
/// </summary>
[Authorize]
public sealed class GomokuHub : Hub
{
    private readonly ISender _mediator;
    private readonly IConnectionTracker _tracker;
    private readonly ILogger<GomokuHub> _logger;

    /// <inheritdoc />
    public GomokuHub(ISender mediator, IConnectionTracker tracker, ILogger<GomokuHub> logger)
    {
        _mediator = mediator;
        _tracker = tracker;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        await _tracker.TrackAsync(Context.ConnectionId, userId);
        using (LogContext.PushProperty("ConnectionId", Context.ConnectionId))
        using (LogContext.PushProperty("UserId", userId.Value))
        {
            _logger.LogInformation("SignalR connection opened");
        }
        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _tracker.UntrackAsync(Context.ConnectionId);
        using (LogContext.PushProperty("ConnectionId", Context.ConnectionId))
        using (LogContext.PushProperty("UserId", Context.UserIdentifier ?? "anonymous"))
        {
            if (exception is not null)
            {
                _logger.LogWarning(exception, "SignalR connection closed with exception");
            }
            else
            {
                _logger.LogInformation("SignalR connection closed");
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>把当前连接加入指定房间的 SignalR group(为了接收该房间的推送);不改 <see cref="Room"/> 聚合。</summary>
    public async Task JoinRoom(Guid roomId)
    {
        var id = new RoomId(roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroupName(id));
        await _tracker.AssociateRoomAsync(Context.ConnectionId, id);
    }

    /// <summary>从指定房间的 SignalR group 中移除当前连接。</summary>
    public async Task LeaveRoom(Guid roomId)
    {
        var id = new RoomId(roomId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroupName(id));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SpectatorsGroupName(id));
        await _tracker.DissociateRoomAsync(Context.ConnectionId, id);
    }

    /// <summary>把当前连接加入某房间的围观者子 group(前端在确认自己是围观者身份后调用)。</summary>
    public Task JoinSpectatorGroup(Guid roomId)
        => Groups.AddToGroupAsync(Context.ConnectionId, SpectatorsGroupName(new RoomId(roomId)));

    /// <summary>落子。</summary>
    public async Task MakeMove(Guid roomId, int row, int col)
    {
        var command = new MakeMoveCommand(GetUserId(), new RoomId(roomId), row, col);
        await _mediator.Send(command, Context.ConnectionAborted);
    }

    /// <summary>发送聊天。</summary>
    public async Task SendChat(Guid roomId, string content, ChatChannel channel)
    {
        var command = new SendChatMessageCommand(GetUserId(), new RoomId(roomId), content, channel);
        await _mediator.Send(command, Context.ConnectionAborted);
    }

    /// <summary>催促对手。</summary>
    public async Task Urge(Guid roomId)
    {
        var command = new UrgeOpponentCommand(GetUserId(), new RoomId(roomId));
        await _mediator.Send(command, Context.ConnectionAborted);
    }

    internal static string RoomGroupName(RoomId id) => $"room:{id.Value}";
    internal static string SpectatorsGroupName(RoomId id) => $"room:{id.Value}:spectators";

    private UserId GetUserId()
    {
        var sub = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? throw new HubException("Missing sub claim.");
        return new UserId(Guid.Parse(sub));
    }
}
