using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.GetMyActiveRooms;

/// <summary>
/// 当前登录用户的活动房间列表(Waiting + Playing,作为玩家参与;不含围观)。
/// 供前端登录后"继续对局"区域使用。不分页(典型 0-5 条)。
/// </summary>
public sealed record GetMyActiveRoomsQuery(UserId UserId)
    : IRequest<IReadOnlyList<RoomSummaryDto>>;
