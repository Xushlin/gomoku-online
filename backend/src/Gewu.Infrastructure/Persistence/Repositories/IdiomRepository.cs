using Gewu.Application.Abstractions;
using Gewu.Domain.Idioms;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="IIdiomRepository"/> 的 EF 实现。
/// <para>
/// 每个方法都是一次走索引的查询。层级过滤统一写成
/// <c>(TierOverride ?? Tier) &lt;= maxTier</c>,EF 会翻成 <c>COALESCE(...)</c>
/// —— 人工校订必须参与筛选,否则 <c>TierOverride</c> 形同虚设。
/// </para>
/// <para>
/// 词典是只读参考数据,所以全部查询都 <c>AsNoTracking</c>:省掉变更跟踪的快照开销,
/// 也从机制上保证游戏侧拿不到能改词典的实体。
/// </para>
/// </summary>
public sealed class IdiomRepository : IIdiomRepository
{
    private readonly AppDbContext _db;

    /// <inheritdoc />
    public IdiomRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public Task<Idiom?> FindByWordAsync(string word, CancellationToken cancellationToken = default)
        => _db.Idioms
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Word == word, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Idiom>> FindContainingCharAsync(
        char character,
        int position,
        IdiomTier maxTier,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // 从 IdiomChars 起手,让 (Char, Position) 索引先把候选缩到几十条,再 join 回 Idioms。
        var query = from c in _db.IdiomChars.AsNoTracking()
                    where c.Char == character && c.Position == position
                    join i in _db.Idioms.AsNoTracking() on c.IdiomId equals i.Id
                    where (i.TierOverride ?? i.Tier) <= maxTier
                    select i;

        return await query.Take(limit).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Idiom>> FindStartingWithCharAsync(
        char character,
        IdiomTier maxTier,
        int limit,
        CancellationToken cancellationToken = default)
        => FindContainingCharAsync(character, 0, maxTier, limit, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Idiom>> GetRandomAsync(
        IdiomTier maxTier,
        int count,
        CancellationToken cancellationToken = default)
    {
        // SQLite 的 RANDOM() 下推到数据库,避免把整层取回内存再洗牌。
        return await _db.Idioms
            .AsNoTracking()
            .Where(i => (i.TierOverride ?? i.Tier) <= maxTier)
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
