using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Puzzles.UsePuzzleHint;

/// <summary>
/// 要一个提示。服务端揭示一个片段并计费。
/// <para>
/// <paramref name="StateJson"/> 是客户端上报的盘面状态(不透明),让服务端能揭示玩家
/// **真正想解的那一格**。它 MUST NOT 参与计分 —— 提示次数只由调用次数决定。
/// </para>
/// </summary>
/// <param name="UserId">调用者。</param>
/// <param name="AttemptId">尝试 id。</param>
/// <param name="StateJson">客户端盘面状态;可为 <c>null</c>。</param>
public sealed record UsePuzzleHintCommand(UserId UserId, Guid AttemptId, string? StateJson = null)
    : IRequest<PuzzleHintDto>;
