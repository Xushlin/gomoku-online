using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Domain.Puzzles;
using MediatR;

namespace Gewu.Application.Features.Puzzles.SubmitPuzzleAttempt;

/// <summary>
/// 提交 handler。通关时:算星 → 标记尝试结束 → 刷新该关最好成绩(只升不降)。
/// 未通关时:记一次错,尝试保持开启,玩家可以继续改。
/// </summary>
public sealed class SubmitPuzzleAttemptCommandHandler
    : IRequestHandler<SubmitPuzzleAttemptCommand, PuzzleSubmitResultDto>
{
    private readonly IPuzzleRepository _puzzles;
    private readonly IPuzzleRulesRegistry _registry;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    /// <inheritdoc />
    public SubmitPuzzleAttemptCommandHandler(
        IPuzzleRepository puzzles,
        IPuzzleRulesRegistry registry,
        IDateTimeProvider clock,
        IUnitOfWork uow)
    {
        _puzzles = puzzles;
        _registry = registry;
        _clock = clock;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<PuzzleSubmitResultDto> Handle(
        SubmitPuzzleAttemptCommand request, CancellationToken cancellationToken)
    {
        var attempt = await _puzzles.FindAttemptAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw new PuzzleNotFoundException($"Attempt '{request.AttemptId}' was not found.");

        var level = await _puzzles.FindLevelByIdAsync(attempt.PuzzleLevelId, cancellationToken)
            ?? throw new PuzzleNotFoundException($"Level {attempt.PuzzleLevelId} was not found.");

        var rules = PuzzleRulesResolver.Resolve(_registry, level.GameKey);
        var validation = rules.Validate(level.SolutionJson, request.SubmissionJson);

        if (!validation.IsCorrect)
        {
            // 答错不结束尝试 —— 玩家继续改。RecordMistake 会拒绝已结束的尝试,
            // 所以重复提交一个已通关的尝试在这里就被挡住了。
            attempt.RecordMistake();
            await _uow.SaveChangesAsync(cancellationToken);

            return new PuzzleSubmitResultDto(
                false, null, null, attempt.Mistakes, attempt.HintsUsed, false);
        }

        var now = _clock.UtcNow;
        var duration = now - attempt.StartedAt;

        // 三个入参全是服务端事实:提示由服务端发放、错误由服务端在 check 里判定、用时取服务端时钟。
        var stars = rules.Score(attempt.HintsUsed, attempt.Mistakes, duration);
        attempt.Complete(stars, now);

        var durationMs = (long)duration.TotalMilliseconds;
        var progress = await _puzzles.FindLevelProgressAsync(
            request.UserId, level.Id, cancellationToken);

        bool newBest;
        if (progress is null)
        {
            await _puzzles.AddLevelProgressAsync(
                PuzzleLevelProgress.First(request.UserId, level.Id, stars, durationMs),
                cancellationToken);
            newBest = true;
        }
        else
        {
            newBest = progress.RecordCompletion(stars, durationMs);
        }

        await _uow.SaveChangesAsync(cancellationToken);

        return new PuzzleSubmitResultDto(
            true, stars, durationMs, attempt.Mistakes, attempt.HintsUsed, newBest);
    }
}
