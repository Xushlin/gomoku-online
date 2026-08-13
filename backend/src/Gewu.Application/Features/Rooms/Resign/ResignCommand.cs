using Gewu.Application.Common.DTOs;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.Resign;

/// <summary>
/// 玩家主动认输,对手胜。允许任意回合调用(包括对手回合)。返回 <see cref="GameEndedDto"/>,
/// 与 SignalR 广播的 <c>GameEnded</c> 事件同形。
/// </summary>
public sealed record ResignCommand(UserId UserId, RoomId RoomId) : IRequest<GameEndedDto>;
