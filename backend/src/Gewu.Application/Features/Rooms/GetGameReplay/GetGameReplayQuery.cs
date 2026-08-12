using Gewu.Application.Common.DTOs;
using Gewu.Domain.Rooms;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetGameReplay;

/// <summary>按房间 Id 拉取 Finished 对局的完整回放。Playing/Waiting 房间请求此 query 会抛 <see cref="Gewu.Application.Common.Exceptions.GameNotFinishedException"/>。</summary>
public sealed record GetGameReplayQuery(RoomId RoomId) : IRequest<GameReplayDto>;
