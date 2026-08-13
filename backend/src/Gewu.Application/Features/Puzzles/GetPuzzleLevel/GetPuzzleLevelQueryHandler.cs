using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Domain.Puzzles;
using MediatR;

namespace Gewu.Application.Features.Puzzles.GetPuzzleLevel;

/// <summary>
/// 单关查询 handler。返回的 <see cref="PuzzleLevelDto"/> 只有布局
/// —— 答案留在服务端,DTO 上没有任何能承载它的字段。
/// </summary>
public sealed class GetPuzzleLevelQueryHandler : IRequestHandler<GetPuzzleLevelQuery, PuzzleLevelDto>
{
    private readonly IPuzzleRepository _puzzles;
    private readonly IPuzzleRulesRegistry _registry;

    /// <inheritdoc />
    public GetPuzzleLevelQueryHandler(IPuzzleRepository puzzles, IPuzzleRulesRegistry registry)
    {
        _puzzles = puzzles;
        _registry = registry;
    }

    /// <inheritdoc />
    public async Task<PuzzleLevelDto> Handle(
        GetPuzzleLevelQuery request, CancellationToken cancellationToken)
    {
        PuzzleRulesResolver.Resolve(_registry, request.GameKey);

        var level = await _puzzles.FindLevelAsync(request.GameKey, request.LevelIndex, cancellationToken)
            ?? throw new PuzzleNotFoundException(
                $"Level {request.LevelIndex} of '{request.GameKey}' was not found.");

        return new PuzzleLevelDto(level.LevelIndex, level.Difficulty, level.LayoutJson);
    }
}
