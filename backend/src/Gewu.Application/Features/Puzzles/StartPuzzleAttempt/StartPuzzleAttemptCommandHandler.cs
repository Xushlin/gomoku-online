using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Domain.Puzzles;
using MediatR;

namespace Gewu.Application.Features.Puzzles.StartPuzzleAttempt;

/// <summary>发起尝试 handler。</summary>
public sealed class StartPuzzleAttemptCommandHandler
    : IRequestHandler<StartPuzzleAttemptCommand, PuzzleAttemptStartedDto>
{
    private readonly IPuzzleRepository _puzzles;
    private readonly IPuzzleRulesRegistry _registry;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    /// <inheritdoc />
    public StartPuzzleAttemptCommandHandler(
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
    public async Task<PuzzleAttemptStartedDto> Handle(
        StartPuzzleAttemptCommand request, CancellationToken cancellationToken)
    {
        PuzzleRulesResolver.Resolve(_registry, request.GameKey);

        var level = await _puzzles.FindLevelAsync(request.GameKey, request.LevelIndex, cancellationToken)
            ?? throw new PuzzleNotFoundException(
                $"Level {request.LevelIndex} of '{request.GameKey}' was not found.");

        var now = _clock.UtcNow;
        var attempt = PuzzleAttempt.Start(Guid.NewGuid(), request.UserId, level.Id, now);

        await _puzzles.AddAttemptAsync(attempt, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return new PuzzleAttemptStartedDto(attempt.Id, level.LevelIndex, level.LayoutJson, now);
    }
}
