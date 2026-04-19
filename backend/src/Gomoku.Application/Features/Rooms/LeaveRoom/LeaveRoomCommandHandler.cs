using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using MediatR;

namespace Gomoku.Application.Features.Rooms.LeaveRoom;

/// <summary>离开房间 handler。区分玩家 / 围观者触发不同事件。</summary>
public sealed class LeaveRoomCommandHandler : IRequestHandler<LeaveRoomCommand, Unit>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;

    /// <inheritdoc />
    public LeaveRoomCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IRoomNotifier notifier)
    {
        _rooms = rooms;
        _users = users;
        _clock = clock;
        _uow = uow;
        _notifier = notifier;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(LeaveRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        var wasPlayer = request.UserId == room.BlackPlayerId
            || (room.WhitePlayerId.HasValue && request.UserId == room.WhitePlayerId.Value);
        var wasSpectator = room.Spectators.Contains(request.UserId);

        room.Leave(request.UserId, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);

        var user = await _users.FindByIdAsync(request.UserId, cancellationToken);
        var leaverDto = new UserSummaryDto(
            request.UserId.Value,
            user?.Username.Value ?? "<unknown>");

        if (wasPlayer)
        {
            await _notifier.PlayerLeftAsync(room.Id, leaverDto, cancellationToken);
        }
        else if (wasSpectator)
        {
            await _notifier.SpectatorLeftAsync(room.Id, leaverDto, cancellationToken);
        }

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);
        var state = room.ToState(usernames);
        await _notifier.RoomStateChangedAsync(room.Id, state, cancellationToken);

        return Unit.Value;
    }
}
