using Gewu.Application.Abstractions;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetRoomRole;

/// <summary>把 <see cref="GetRoomRoleQuery"/> 解析成聚合里的身份。</summary>
public sealed class GetRoomRoleQueryHandler : IRequestHandler<GetRoomRoleQuery, RoomRole>
{
    private readonly IRoomRepository _rooms;

    /// <inheritdoc />
    public GetRoomRoleQueryHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    /// <inheritdoc />
    public async Task<RoomRole> Handle(GetRoomRoleQuery request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken);
        if (room is null)
        {
            return RoomRole.None;
        }

        if (room.IsPlayer(request.UserId))
        {
            return RoomRole.Player;
        }

        return room.IsSpectator(request.UserId) ? RoomRole.Spectator : RoomRole.None;
    }
}
