using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Mapping;
using MediatR;

namespace Gomoku.Application.Features.Rooms.GetMyActiveRooms;

/// <summary>
/// 调仓储拉用户活动房间 → 收集所有 UserId(Host / Black / White)一次 lookup →
/// 映射为 <see cref="RoomSummaryDto"/> 数组(复用 <c>RoomMapping.ToSummary</c>)。
/// </summary>
public sealed class GetMyActiveRoomsQueryHandler
    : IRequestHandler<GetMyActiveRoomsQuery, IReadOnlyList<RoomSummaryDto>>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;

    /// <inheritdoc />
    public GetMyActiveRoomsQueryHandler(IRoomRepository rooms, IUserRepository users)
    {
        _rooms = rooms;
        _users = users;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoomSummaryDto>> Handle(
        GetMyActiveRoomsQuery request, CancellationToken cancellationToken)
    {
        var rooms = await _rooms.GetActiveRoomsByUserAsync(request.UserId, cancellationToken);
        if (rooms.Count == 0)
        {
            return Array.Empty<RoomSummaryDto>();
        }

        var allIds = rooms.SelectMany(r => r.CollectUserIds()).Distinct().ToList();
        var usernames = await _users.LookupUsernamesAsync(allIds, cancellationToken);

        return rooms
            .Select(r => r.ToSummary(usernames))
            .ToList()
            .AsReadOnly();
    }
}
