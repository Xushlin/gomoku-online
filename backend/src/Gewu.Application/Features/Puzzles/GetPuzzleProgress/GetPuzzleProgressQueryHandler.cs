using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Domain.Puzzles;
using MediatR;

namespace Gewu.Application.Features.Puzzles.GetPuzzleProgress;

/// <summary>
/// 进度 handler。已解锁下标 = 已完成关卡的最大序号 + 1;总星数 = 各关最好星级之和。
/// 两者都是就地算出来的 —— 库里没有对应的计数器列,也就没有跟事实不一致的可能。
/// </summary>
public sealed class GetPuzzleProgressQueryHandler
    : IRequestHandler<GetPuzzleProgressQuery, PuzzleProgressDto>
{
    private readonly IPuzzleRepository _puzzles;
    private readonly IPuzzleRulesRegistry _registry;

    /// <inheritdoc />
    public GetPuzzleProgressQueryHandler(IPuzzleRepository puzzles, IPuzzleRulesRegistry registry)
    {
        _puzzles = puzzles;
        _registry = registry;
    }

    /// <inheritdoc />
    public async Task<PuzzleProgressDto> Handle(
        GetPuzzleProgressQuery request, CancellationToken cancellationToken)
    {
        PuzzleRulesResolver.Resolve(_registry, request.GameKey);

        var progress = await _puzzles.ListLevelProgressAsync(
            request.UserId, request.GameKey, cancellationToken);

        var unlockedIndex = progress.Count == 0 ? 0 : progress.Keys.Max() + 1;
        var totalStars = progress.Values.Sum(p => p.BestStars);

        return new PuzzleProgressDto(
            request.GameKey, unlockedIndex, totalStars, progress.Count);
    }
}
