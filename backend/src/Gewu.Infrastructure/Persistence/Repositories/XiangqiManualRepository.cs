using Gewu.Application.Abstractions;
using Gewu.Domain.Manuals;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="IXiangqiManualRepository"/> 的 EF 实现。全程 <c>AsNoTracking</c> ——
/// 古谱是只读资料,运行期没有任何路径会改它。
/// </summary>
public sealed class XiangqiManualRepository : IXiangqiManualRepository
{
    private readonly AppDbContext _db;

    /// <inheritdoc />
    public XiangqiManualRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<(XiangqiManual Manual, int LineCount)>> ListManualsAsync(
        CancellationToken ct = default)
    {
        var manuals = await _db.XiangqiManuals.AsNoTracking().OrderBy(m => m.Key).ToListAsync(ct);
        var counts = await _db.XiangqiManualLines
            .AsNoTracking()
            .GroupBy(l => l.ManualKey)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byKey = counts.ToDictionary(x => x.Key, x => x.Count);
        return manuals.Select(m => (m, byKey.TryGetValue(m.Key, out var n) ? n : 0)).ToList();
    }

    /// <inheritdoc />
    public async Task<XiangqiManual?> GetManualAsync(string manualKey, CancellationToken ct = default)
        => await _db.XiangqiManuals.AsNoTracking().FirstOrDefaultAsync(m => m.Key == manualKey, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<XiangqiManualLine>> ListLinesAsync(
        string manualKey, CancellationToken ct = default)
        => await _db.XiangqiManualLines
            .AsNoTracking()
            .Where(l => l.ManualKey == manualKey)
            .OrderBy(l => l.Chapter)
            .ThenBy(l => l.OrderInChapter)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<XiangqiManualLine?> GetLineAsync(int id, CancellationToken ct = default)
        => await _db.XiangqiManualLines
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, ct);
}
