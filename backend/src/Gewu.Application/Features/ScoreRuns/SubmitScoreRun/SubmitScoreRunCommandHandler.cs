using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Domain.Games.Tetris;
using MediatR;

namespace Gewu.Application.Features.ScoreRuns.SubmitScoreRun;

/// <summary>
/// 结算 handler:取回自己的 run → 重放放置序列 → 把重放算出的三个数字写进 run。
/// <para>
/// 顺序是**先重放,后写入**。非法放置会让 <c>Replay</c> 抛出,run 于是保持未结算 ——
/// 一局提交失败的游戏不该把这一局的机会用掉。
/// </para>
/// <para>
/// 重放保证的是**分数与放置一致**,不是**放置出自人手**:离线求解器可以按服务端给的种子算出
/// 接近最优的下法。这一条不假装被解决了(<c>add-xiangqi-ai</c> 立下的规矩:一个无法验证的断言
/// 比没有断言更糟),而分数也**不设上限** —— 任何硬上限都会先误伤真高手。
/// </para>
/// </summary>
public sealed class SubmitScoreRunCommandHandler
    : IRequestHandler<SubmitScoreRunCommand, ScoreRunResultDto>
{
    private readonly IScoreRunRepository _runs;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    /// <inheritdoc />
    public SubmitScoreRunCommandHandler(
        IScoreRunRepository runs, IDateTimeProvider clock, IUnitOfWork uow)
    {
        _runs = runs;
        _clock = clock;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<ScoreRunResultDto> Handle(
        SubmitScoreRunCommand request, CancellationToken cancellationToken)
    {
        // 所有权在查询条件里 —— 别人的 run 与不存在的 run 是同一个结果。
        var run = await _runs.FindAsync(request.RunId, request.UserId, cancellationToken)
            ?? throw new ScoreRunNotFoundException($"Run '{request.RunId}' was not found.");

        var placements = request.Placements
            .Select(p => new TetrisPlacement(p.Rotation, p.Column))
            .ToList()
            .AsReadOnly();

        var (score, lines, level) = ScoreAttackGames.Replay(run.GameKey, run.Seed, placements);

        var now = _clock.UtcNow;
        // Finish 拒绝已结算的 run —— 重复提交在这里被挡住,而不是靠调用方记得先查。
        run.Finish(score, lines, level, now);
        await _uow.SaveChangesAsync(cancellationToken);

        return new ScoreRunResultDto(
            run.Id, score, lines, level, placements.Count,
            (long)(now - run.StartedAt).TotalMilliseconds);
    }
}
