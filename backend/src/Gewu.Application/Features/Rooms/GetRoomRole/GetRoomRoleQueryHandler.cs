using Gewu.Application.Abstractions;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetRoomRole;

/// <summary>把 <see cref="GetRoomRoleQuery"/> 解析成聚合里的身份。</summary>
public sealed class GetRoomRoleQueryHandler : IRequestHandler<GetRoomRoleQuery, RoomMembership>
{
    private readonly IRoomRepository _rooms;

    /// <inheritdoc />
    public GetRoomRoleQueryHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    /// <inheritdoc />
    public async Task<RoomMembership> Handle(GetRoomRoleQuery request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken);
        if (room is null)
        {
            return RoomMembership.None;
        }

        // 座位号与"是不是玩家"由同一次查询给出 —— 两次分别问会有它们不一致的可能,
        // 而不一致的后果是某个连接被放进一个不存在的座位群、或者一个群都不进。
        if (room.SeatOf(request.UserId) is int seat)
        {
            return RoomMembership.AtSeat(seat);
        }

        return room.IsSpectator(request.UserId) ? RoomMembership.Spectator : RoomMembership.None;
    }
}
