using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Domain.Puzzles;
using MediatR;

namespace Gewu.Application.Features.Puzzles.GetPuzzleLevels;

/// <summary>
/// 关卡列表 handler。解锁规则:序号 0 恒解锁,其余需要前一关已通关
/// —— 由最好成绩表推导,没有"已解锁下标"这种存储列。
/// </summary>
public sealed class GetPuzzleLevelsQueryHandler
    : IRequestHandler<GetPuzzleLevelsQuery, IReadOnlyList<PuzzleLevelSummaryDto>>
{
    private readonly IPuzzleRepository _puzzles;
    private readonly IPuzzleRulesRegistry _registry;

    /// <inheritdoc />
    public GetPuzzleLevelsQueryHandler(IPuzzleRepository puzzles, IPuzzleRulesRegistry registry)
    {
        _puzzles = puzzles;
        _registry = registry;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PuzzleLevelSummaryDto>> Handle(
        GetPuzzleLevelsQuery request, CancellationToken cancellationToken)
    {
        PuzzleRulesResolver.Resolve(_registry, request.GameKey);

        var levels = await _puzzles.ListLevelsAsync(request.GameKey, cancellationToken);
        var progress = await _puzzles.ListLevelProgressAsync(
            request.UserId, request.GameKey, cancellationToken);

        var result = new List<PuzzleLevelSummaryDto>(levels.Count);
        foreach (var level in levels)
        {
            progress.TryGetValue(level.LevelIndex, out var best);

            // 解锁 = 第一关,或前一关已有最好成绩(即已通关过)。
            var unlocked = level.LevelIndex == 0
                || progress.ContainsKey(level.LevelIndex - 1);

            result.Add(new PuzzleLevelSummaryDto(
                level.LevelIndex,
                level.Difficulty,
                unlocked,
                best?.BestStars,
                best?.BestDurationMs));
        }

        return result;
    }
}
