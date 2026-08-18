using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Domain.ScoreRuns;
using MediatR;

namespace Gewu.Application.Features.ScoreRuns.StartScoreRun;

/// <summary>
/// 开局 handler:生成种子 → 落库一条未结算的 run → 把 id 与种子交给客户端。
/// <para>
/// 键不是计分类游戏时 **404**,而不是 400 —— "这个游戏不存在"跟"你传的参数格式不对"是两件事,
/// 与 <c>StartPuzzleAttemptCommandValidator</c> 上写的同一条理由。
/// </para>
/// </summary>
public sealed class StartScoreRunCommandHandler
    : IRequestHandler<StartScoreRunCommand, ScoreRunStartedDto>
{
    private readonly IScoreRunRepository _runs;
    private readonly ISeedProvider _seeds;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    /// <inheritdoc />
    public StartScoreRunCommandHandler(
        IScoreRunRepository runs,
        ISeedProvider seeds,
        IDateTimeProvider clock,
        IUnitOfWork uow)
    {
        _runs = runs;
        _seeds = seeds;
        _clock = clock;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<ScoreRunStartedDto> Handle(
        StartScoreRunCommand request, CancellationToken cancellationToken)
    {
        // 「能开 run」与「能重放」读同一个事实。分成两份判断就是 enforce-ai-availability
        // 那个缺陷的形状:端点接受了一个后台永远处理不了的状态。
        if (!ScoreAttackGames.IsScoreAttackGame(request.GameKey))
        {
            throw new ScoreRunNotFoundException(
                $"'{request.GameKey}' is not a score-attack game on this platform.");
        }

        var now = _clock.UtcNow;
        var run = ScoreRun.Start(
            Guid.NewGuid(), request.UserId, request.GameKey, _seeds.NextSeed(), now);

        await _runs.AddAsync(run, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return new ScoreRunStartedDto(run.Id, run.GameKey, run.Seed, now);
    }
}
