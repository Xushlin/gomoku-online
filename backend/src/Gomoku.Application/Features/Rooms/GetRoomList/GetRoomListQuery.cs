using Gomoku.Application.Common.DTOs;
using MediatR;

namespace Gomoku.Application.Features.Rooms.GetRoomList;

/// <summary>查询所有活跃(Waiting / Playing)房间的摘要。</summary>
public sealed record GetRoomListQuery : IRequest<IReadOnlyList<RoomSummaryDto>>;
