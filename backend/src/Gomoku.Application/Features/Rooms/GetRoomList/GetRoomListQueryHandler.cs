using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Mapping;
using MediatR;

namespace Gomoku.Application.Features.Rooms.GetRoomList;

/// <summary>活跃房间列表查询 handler。</summary>
public sealed class GetRoomListQueryHandler : IRequestHandler<GetRoomListQuery, IReadOnlyList<RoomSummaryDto>>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;

    /// <inheritdoc />
    public GetRoomListQueryHandler(IRoomRepository rooms, IUserRepository users)
    {
        _rooms = rooms;
        _users = users;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoomSummaryDto>> Handle(
        GetRoomListQuery request,
        CancellationToken cancellationToken)
    {
        var rooms = await _rooms.GetActiveRoomsAsync(cancellationToken);
        if (rooms.Count == 0)
        {
            return Array.Empty<RoomSummaryDto>();
        }

        var allIds = rooms.SelectMany(r => r.CollectUserIds()).Distinct();
        var usernames = await _users.LookupUsernamesAsync(allIds, cancellationToken);

        return rooms.Select(r => r.ToSummary(usernames)).ToList();
    }
}
