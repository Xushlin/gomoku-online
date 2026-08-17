using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gewu.Application.Features.Rooms.JoinRoom;

/// <summary>加入房间为白方并启动对局;广播 <c>PlayerJoined</c> + <c>RoomStateChanged</c>。</summary>
public sealed class JoinRoomCommandHandler : IRequestHandler<JoinRoomCommand, RoomStateDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;
    private readonly GameOptions _gameOptions;

    /// <inheritdoc />
    public JoinRoomCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IRoomNotifier notifier,
        IOptions<GameOptions> gameOptions)
    {
        _rooms = rooms;
        _users = users;
        _clock = clock;
        _uow = uow;
        _notifier = notifier;
        _gameOptions = gameOptions.Value;
    }

    /// <inheritdoc />
    public async Task<RoomStateDto> Handle(JoinRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        room.JoinAsPlayer(request.UserId, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);
        var state = room.ToState(usernames, _gameOptions.TurnTimeoutSeconds, RoomView.For(room, request.UserId));
        var joiner = new UserSummaryDto(request.UserId.Value,
            usernames.TryGetValue(request.UserId.Value, out var n) ? n : "<unknown>");

        await _notifier.PlayerJoinedAsync(room.Id, joiner, cancellationToken);
        await _notifier.RoomStateChangedAsync(room, usernames, _gameOptions.TurnTimeoutSeconds, cancellationToken);

        return state;
    }
}
