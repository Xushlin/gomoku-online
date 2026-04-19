using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.UrgeOpponent;

/// <summary>催促对手下棋(冷却 30 秒,仅玩家)。</summary>
public sealed record UrgeOpponentCommand(UserId UserId, RoomId RoomId) : IRequest<UrgeDto>;
