using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using MediatR;

namespace Gomoku.Application.Features.Rooms.LeaveAsSpectator;

/// <summary>围观者离开 handler。广播 <c>SpectatorLeft</c> + <c>RoomStateChanged</c>(D18 对称)。</summary>
public sealed class LeaveAsSpectatorCommandHandler : IRequestHandler<LeaveAsSpectatorCommand, Unit>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;

    /// <inheritdoc />
    public LeaveAsSpectatorCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IUnitOfWork uow,
        IRoomNotifier notifier)
    {
        _rooms = rooms;
        _users = users;
        _uow = uow;
        _notifier = notifier;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(LeaveAsSpectatorCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        room.LeaveAsSpectator(request.UserId);
        await _uow.SaveChangesAsync(cancellationToken);

        var user = await _users.FindByIdAsync(request.UserId, cancellationToken);
        var leaverDto = new UserSummaryDto(
            request.UserId.Value,
            user?.Username.Value ?? "<unknown>");

        await _notifier.SpectatorLeftAsync(room.Id, leaverDto, cancellationToken);

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);
        var state = room.ToState(usernames);
        await _notifier.RoomStateChangedAsync(room.Id, state, cancellationToken);

        return Unit.Value;
    }
}
