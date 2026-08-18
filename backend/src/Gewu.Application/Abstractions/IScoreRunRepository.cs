using Gewu.Domain.ScoreRuns;
using Gewu.Domain.Users;

namespace Gewu.Application.Abstractions;

/// <summary>
/// 分数榜的一行 —— 某玩家在窗口内最好的那一局。
/// <para>
/// 是投影而不是实体:榜要的是"每人一行",而那在 SQL 里是一次分组去重,取回整批 run
/// 再在内存里挑最高分会把过滤和分页都搬到进程里。
/// </para>
/// </summary>
/// <param name="UserId">玩家。</param>
/// <param name="Score">最高分。</param>
/// <param name="Lines">那一局的消行数。</param>
/// <param name="Level">那一局的结束等级。</param>
/// <param name="FinishedAt">那一局的结算时刻。</param>
public readonly record struct ScoreStanding(
    UserId UserId, int Score, int Lines, int Level, DateTime FinishedAt);

/// <summary>计分类 run 的持久化口。</summary>
public interface IScoreRunRepository
{
    /// <summary>新开一局。</summary>
    /// <param name="run">run。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task AddAsync(ScoreRun run, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 <c>(run id, 所有者)</c> 取一局 —— 所有权是**查询条件的一部分**,
    /// 所以"别人的 run"和"不存在的 run"对调用方是同一个结果。与 <c>IPuzzleRepository</c> 同规。
    /// </summary>
    /// <param name="runId">run id。</param>
    /// <param name="userId">调用者。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<ScoreRun?> FindAsync(
        Guid runId, UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页拉某游戏的分数榜:每个玩家一行(窗口内最高分),按分数降序。
    /// </summary>
    /// <param name="gameKey">游戏键。</param>
    /// <param name="finishedAtOrAfter">窗口起始时刻(含);<c>null</c> 表示不按时间过滤。</param>
    /// <param name="page">页码,从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<(IReadOnlyList<ScoreStanding> Entries, int Total)> GetLeaderboardPagedAsync(
        string gameKey,
        DateTime? finishedAtOrAfter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
