using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gomoku.Application.Features.Rooms.UrgeOpponent;

/// <summary>催促 handler。调 <c>Room.UrgeOpponent</c>,仅推给被催方。</summary>
public sealed class UrgeOpponentCommandHandler : IRequestHandler<UrgeOpponentCommand, UrgeDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;
    private readonly RoomsOptions _options;

    /// <inheritdoc />
    public UrgeOpponentCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IRoomNotifier notifier,
        IOptions<RoomsOptions> options)
    {
        _rooms = rooms;
        _users = users;
        _clock = clock;
        _uow = uow;
        _notifier = notifier;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<UrgeDto> Handle(UrgeOpponentCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        var sender = await _users.FindByIdAsync(request.UserId, cancellationToken)
            ?? throw new UserNotFoundException($"User '{request.UserId.Value}' was not found.");

        var now = _clock.UtcNow;
        var outcome = room.UrgeOpponent(request.UserId, now, _options.UrgeCooldownSeconds);
        await _uow.SaveChangesAsync(cancellationToken);

        var dto = new UrgeDto(sender.Id.Value, sender.Username.Value, now);
        await _notifier.OpponentUrgedAsync(room.Id, outcome.UrgedUser, dto, cancellationToken);

        return dto;
    }
}
