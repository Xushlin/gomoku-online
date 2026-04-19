using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.JoinAsSpectator;

/// <summary>用户加入房间作为围观者。</summary>
public sealed record JoinAsSpectatorCommand(UserId UserId, RoomId RoomId) : IRequest<Unit>;
