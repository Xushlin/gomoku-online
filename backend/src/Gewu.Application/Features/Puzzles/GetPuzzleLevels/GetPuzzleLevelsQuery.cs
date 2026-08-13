using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Puzzles.GetPuzzleLevels;

/// <summary>取某游戏的关卡列表,附带调用者的最好成绩与解锁状态。</summary>
/// <param name="UserId">调用者。</param>
/// <param name="GameKey">游戏键。</param>
public sealed record GetPuzzleLevelsQuery(UserId UserId, string GameKey)
    : IRequest<IReadOnlyList<PuzzleLevelSummaryDto>>;
