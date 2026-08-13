using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Domain.Puzzles;
using MediatR;

namespace Gewu.Application.Features.Puzzles.UsePuzzleHint;

/// <summary>
/// 提示 handler。响应只含被揭示的那一个片段 —— 答案其余部分不出服务端。
/// </summary>
public sealed class UsePuzzleHintCommandHandler : IRequestHandler<UsePuzzleHintCommand, PuzzleHintDto>
{
    private readonly IPuzzleRepository _puzzles;
    private readonly IPuzzleRulesRegistry _registry;
    private readonly IUnitOfWork _uow;

    /// <inheritdoc />
    public UsePuzzleHintCommandHandler(
        IPuzzleRepository puzzles, IPuzzleRulesRegistry registry, IUnitOfWork uow)
    {
        _puzzles = puzzles;
        _registry = registry;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<PuzzleHintDto> Handle(
        UsePuzzleHintCommand request, CancellationToken cancellationToken)
    {
        var attempt = await _puzzles.FindAttemptAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw new PuzzleNotFoundException($"Attempt '{request.AttemptId}' was not found.");

        var level = await _puzzles.FindLevelByIdAsync(attempt.PuzzleLevelId, cancellationToken)
            ?? throw new PuzzleNotFoundException($"Level {attempt.PuzzleLevelId} was not found.");

        var rules = PuzzleRulesResolver.Resolve(_registry, level.GameKey);

        // 先记账再揭示:RecordHint 会拒绝一个已结束的尝试,所以提交之后要不到提示。
        attempt.RecordHint();
        var hint = rules.Hint(level.SolutionJson, level.LayoutJson, attempt.HintsUsed - 1);

        await _uow.SaveChangesAsync(cancellationToken);

        return new PuzzleHintDto(hint.RevealedJson, attempt.HintsUsed);
    }
}
