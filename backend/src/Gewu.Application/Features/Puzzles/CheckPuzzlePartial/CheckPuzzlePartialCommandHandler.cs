using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Domain.Puzzles;
using MediatR;

namespace Gewu.Application.Features.Puzzles.CheckPuzzlePartial;

/// <summary>部分校验 handler。</summary>
public sealed class CheckPuzzlePartialCommandHandler
    : IRequestHandler<CheckPuzzlePartialCommand, PuzzleCheckResultDto>
{
    private readonly IPuzzleRepository _puzzles;
    private readonly IPuzzleRulesRegistry _registry;
    private readonly IUnitOfWork _uow;

    /// <inheritdoc />
    public CheckPuzzlePartialCommandHandler(
        IPuzzleRepository puzzles, IPuzzleRulesRegistry registry, IUnitOfWork uow)
    {
        _puzzles = puzzles;
        _registry = registry;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<PuzzleCheckResultDto> Handle(
        CheckPuzzlePartialCommand request, CancellationToken cancellationToken)
    {
        // 所有权是查询条件的一部分:别人的尝试与不存在的尝试对调用方是同一个结果(404)。
        var attempt = await _puzzles.FindAttemptAsync(request.AttemptId, request.UserId, cancellationToken)
            ?? throw new PuzzleNotFoundException($"Attempt '{request.AttemptId}' was not found.");

        var level = await _puzzles.FindLevelByIdAsync(attempt.PuzzleLevelId, cancellationToken)
            ?? throw new PuzzleNotFoundException($"Level {attempt.PuzzleLevelId} was not found.");

        var rules = PuzzleRulesResolver.Resolve(_registry, level.GameKey);
        var result = rules.CheckPartial(
            level.SolutionJson, level.LayoutJson, request.PartialJson);

        if (!result.IsCorrect)
        {
            attempt.RecordMistake();
            await _uow.SaveChangesAsync(cancellationToken);

            // 答错时**不**转发载荷,即便规则实现填了 —— 否则错误路径就成了泄题通道。
            return new PuzzleCheckResultDto(false, attempt.Mistakes, PayloadJson: null);
        }

        return new PuzzleCheckResultDto(true, attempt.Mistakes, result.PayloadJson);
    }
}
