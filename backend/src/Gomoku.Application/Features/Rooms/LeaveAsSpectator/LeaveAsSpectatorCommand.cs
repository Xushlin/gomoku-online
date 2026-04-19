using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.LeaveAsSpectator;

/// <summary>用户从围观者集合中离开。</summary>
public sealed record LeaveAsSpectatorCommand(UserId UserId, RoomId RoomId) : IRequest<Unit>;
