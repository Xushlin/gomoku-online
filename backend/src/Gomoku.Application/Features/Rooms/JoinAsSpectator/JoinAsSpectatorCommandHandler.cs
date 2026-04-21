using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gomoku.Application.Features.Rooms.JoinAsSpectator;

/// <summary>围观者加入 handler。广播 <c>SpectatorJoined</c> + <c>RoomStateChanged</c>。</summary>
public sealed class JoinAsSpectatorCommandHandler : IRequestHandler<JoinAsSpectatorCommand, Unit>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;
    private readonly GameOptions _gameOptions;

    /// <inheritdoc />
    public JoinAsSpectatorCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IUnitOfWork uow,
        IRoomNotifier notifier,
        IOptions<GameOptions> gameOptions)
    {
        _rooms = rooms;
        _users = users;
        _uow = uow;
        _notifier = notifier;
        _gameOptions = gameOptions.Value;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(JoinAsSpectatorCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        room.JoinAsSpectator(request.UserId);
        await _uow.SaveChangesAsync(cancellationToken);

        var user = await _users.FindByIdAsync(request.UserId, cancellationToken);
        var spectatorDto = new UserSummaryDto(
            request.UserId.Value,
            user?.Username.Value ?? "<unknown>");

        await _notifier.SpectatorJoinedAsync(room.Id, spectatorDto, cancellationToken);

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);
        var state = room.ToState(usernames, _gameOptions.TurnTimeoutSeconds);
        await _notifier.RoomStateChangedAsync(room.Id, state, cancellationToken);

        return Unit.Value;
    }
}
