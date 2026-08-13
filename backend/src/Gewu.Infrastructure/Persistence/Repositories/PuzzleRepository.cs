using Gewu.Application.Abstractions;
using Gewu.Domain.Puzzles;
using Gewu.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="IPuzzleRepository"/> 的 EF 实现。
/// <para>
/// 关卡是只读参考数据,读取走 <c>AsNoTracking</c>;尝试与最好成绩要被改,因此**保持跟踪**
/// —— handler 调完领域方法后由 <c>IUnitOfWork</c> 一次提交。
/// </para>
/// </summary>
public sealed class PuzzleRepository : IPuzzleRepository
{
    private readonly AppDbContext _db;

    /// <inheritdoc />
    public PuzzleRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PuzzleLevel>> ListLevelsAsync(
        string gameKey, CancellationToken cancellationToken = default)
        => await _db.PuzzleLevels
            .AsNoTracking()
            .Where(l => l.GameKey == gameKey)
            .OrderBy(l => l.LevelIndex)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<PuzzleLevel?> FindLevelAsync(
        string gameKey, int levelIndex, CancellationToken cancellationToken = default)
        => _db.PuzzleLevels
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.GameKey == gameKey && l.LevelIndex == levelIndex, cancellationToken);

    /// <inheritdoc />
    public Task<PuzzleLevel?> FindLevelByIdAsync(
        int puzzleLevelId, CancellationToken cancellationToken = default)
        => _db.PuzzleLevels
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == puzzleLevelId, cancellationToken);

    /// <inheritdoc />
    public Task<PuzzleAttempt?> FindAttemptAsync(
        Guid attemptId, UserId userId, CancellationToken cancellationToken = default)
        => _db.PuzzleAttempts
            .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, cancellationToken);

    /// <inheritdoc />
    public async Task AddAttemptAsync(
        PuzzleAttempt attempt, CancellationToken cancellationToken = default)
        => await _db.PuzzleAttempts.AddAsync(attempt, cancellationToken);

    /// <inheritdoc />
    public Task<PuzzleLevelProgress?> FindLevelProgressAsync(
        UserId userId, int puzzleLevelId, CancellationToken cancellationToken = default)
        => _db.PuzzleLevelProgress
            .FirstOrDefaultAsync(
                p => p.UserId == userId && p.PuzzleLevelId == puzzleLevelId, cancellationToken);

    /// <inheritdoc />
    public async Task AddLevelProgressAsync(
        PuzzleLevelProgress progress, CancellationToken cancellationToken = default)
        => await _db.PuzzleLevelProgress.AddAsync(progress, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, PuzzleLevelProgress>> ListLevelProgressAsync(
        UserId userId, string gameKey, CancellationToken cancellationToken = default)
    {
        // 按 LevelIndex 建键(而非 PuzzleLevelId):调用方关心的是"第几关",
        // 解锁判断也要拿相邻序号比较。
        var rows = await (
            from p in _db.PuzzleLevelProgress
            join l in _db.PuzzleLevels on p.PuzzleLevelId equals l.Id
            where p.UserId == userId && l.GameKey == gameKey
            select new { l.LevelIndex, Progress = p })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.LevelIndex, r => r.Progress);
    }
}
