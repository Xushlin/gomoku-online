using Gewu.Application.Abstractions;
using Gewu.Domain.ScoreRuns;
using Gewu.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Persistence.Repositories;

/// <summary><see cref="IScoreRunRepository"/> 的 EF 实现。</summary>
public sealed class ScoreRunRepository : IScoreRunRepository
{
    private readonly AppDbContext _db;

    /// <inheritdoc />
    public ScoreRunRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task AddAsync(ScoreRun run, CancellationToken cancellationToken = default)
        => await _db.ScoreRuns.AddAsync(run, cancellationToken);

    /// <inheritdoc />
    public Task<ScoreRun?> FindAsync(
        Guid runId, UserId userId, CancellationToken cancellationToken = default)
        => _db.ScoreRuns
            .FirstOrDefaultAsync(r => r.Id == runId && r.UserId == userId, cancellationToken);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ScoreStanding> Entries, int Total)> GetLeaderboardPagedAsync(
        string gameKey,
        DateTime? finishedAtOrAfter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var finished = _db.ScoreRuns.Where(r => r.GameKey == gameKey && r.FinishedAt != null);

        if (finishedAtOrAfter is not null)
        {
            finished = finished.Where(r => r.FinishedAt >= finishedAtOrAfter);
        }

        // 每人一行:留下"同一个玩家没有任何一局比它更好"的那一局。
        //
        // 这是 top-1-per-group 的相关子查询写法,而不是 GroupBy(...).Select(g => g.OrderBy(...).First())
        // —— 后者在 SQLite 上不保证被翻译,一旦退化成客户端求值,过滤和分页就都搬到了进程里。
        // 「更好」的比较必须是**全序**,否则同一个玩家会占两行:分数 → 结算更早 → id。
        // 前两级几乎足够,第三级是为了让"完全同分且同一毫秒"也只留一行 —— 榜上出现同名两行,
        // 是那种看一眼就知道错了、却很难说清为什么的 bug。
        var best = finished.Where(r => !finished.Any(o =>
            o.UserId == r.UserId
            && (o.Score > r.Score
                || (o.Score == r.Score && o.FinishedAt < r.FinishedAt)
                || (o.Score == r.Score && o.FinishedAt == r.FinishedAt && o.Id < r.Id))));

        var total = await best.CountAsync(cancellationToken);

        var entries = await best
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.FinishedAt)
            .ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ScoreStanding(
                r.UserId, r.Score!.Value, r.Lines!.Value, r.Level!.Value, r.FinishedAt!.Value))
            .ToListAsync(cancellationToken);

        return (entries.AsReadOnly(), total);
    }
}
