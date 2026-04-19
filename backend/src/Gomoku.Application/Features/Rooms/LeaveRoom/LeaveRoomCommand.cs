using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.LeaveRoom;

/// <summary>玩家 / 围观者离开房间。按角色分别广播 <c>PlayerLeft</c> 或 <c>SpectatorLeft</c>。</summary>
public sealed record LeaveRoomCommand(UserId UserId, RoomId RoomId) : IRequest<Unit>;
