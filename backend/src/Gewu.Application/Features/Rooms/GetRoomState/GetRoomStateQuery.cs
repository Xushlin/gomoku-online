using Gewu.Application.Common.DTOs;
using Gewu.Domain.Rooms;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetRoomState;

/// <summary>按 RoomId 查询完整房间状态(含所有 Moves / ChatMessages / Spectators)。</summary>
public sealed record GetRoomStateQuery(RoomId RoomId) : IRequest<RoomStateDto>;
