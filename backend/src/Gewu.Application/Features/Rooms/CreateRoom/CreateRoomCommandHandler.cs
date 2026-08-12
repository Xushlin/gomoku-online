using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using Gomoku.Domain.Rooms;
using MediatR;

namespace Gomoku.Application.Features.Rooms.CreateRoom;

/// <summary>创建房间 handler。</summary>
public sealed class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, RoomSummaryDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    /// <inheritdoc />
    public CreateRoomCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IDateTimeProvider clock,
        IUnitOfWork uow)
    {
        _rooms = rooms;
        _users = users;
        _clock = clock;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<RoomSummaryDto> Handle(CreateRoomCommand request, CancellationToken cancellationToken)
    {
        var host = await _users.FindByIdAsync(request.HostUserId, cancellationToken)
            ?? throw new UserNotFoundException($"User '{request.HostUserId.Value}' was not found.");

        var room = Room.Create(RoomId.NewId(), request.Name, request.HostUserId, _clock.UtcNow);

        await _rooms.AddAsync(room, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var usernames = new Dictionary<Guid, string> { [host.Id.Value] = host.Username.Value };
        return room.ToSummary(usernames);
    }
}
