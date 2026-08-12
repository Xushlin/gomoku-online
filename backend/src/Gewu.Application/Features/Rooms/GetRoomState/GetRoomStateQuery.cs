using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Rooms;
using MediatR;

namespace Gomoku.Application.Features.Rooms.GetRoomState;

/// <summary>按 RoomId 查询完整房间状态(含所有 Moves / ChatMessages / Spectators)。</summary>
public sealed record GetRoomStateQuery(RoomId RoomId) : IRequest<RoomStateDto>;
