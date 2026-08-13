using Gewu.Application.Common.DTOs;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.UrgeOpponent;

/// <summary>催促对手下棋(冷却 30 秒,仅玩家)。</summary>
public sealed record UrgeOpponentCommand(UserId UserId, RoomId RoomId) : IRequest<UrgeDto>;
