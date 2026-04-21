using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gomoku.Application.Features.Rooms.GetRoomState;

/// <summary>完整房间状态查询 handler。</summary>
public sealed class GetRoomStateQueryHandler : IRequestHandler<GetRoomStateQuery, RoomStateDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly GameOptions _gameOptions;

    /// <inheritdoc />
    public GetRoomStateQueryHandler(IRoomRepository rooms, IUserRepository users, IOptions<GameOptions> gameOptions)
    {
        _rooms = rooms;
        _users = users;
        _gameOptions = gameOptions.Value;
    }

    /// <inheritdoc />
    public async Task<RoomStateDto> Handle(GetRoomStateQuery request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        var usernames = await _users.LookupUsernamesAsync(room.CollectUserIds(), cancellationToken);
        return room.ToState(usernames, _gameOptions.TurnTimeoutSeconds);
    }
}
