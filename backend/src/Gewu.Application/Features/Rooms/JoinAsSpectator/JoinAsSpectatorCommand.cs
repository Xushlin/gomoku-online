using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.JoinAsSpectator;

/// <summary>用户加入房间作为围观者。</summary>
public sealed record JoinAsSpectatorCommand(UserId UserId, RoomId RoomId) : IRequest<Unit>;
