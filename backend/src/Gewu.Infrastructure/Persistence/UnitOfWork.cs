using Gewu.Application.Abstractions;

namespace Gewu.Infrastructure.Persistence;

/// <summary>对 <see cref="AppDbContext.SaveChangesAsync(CancellationToken)"/> 的薄封装。</summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;

    /// <inheritdoc />
    public UnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
