using Gomoku.Application.Abstractions;

namespace Gomoku.Infrastructure.Persistence;

/// <summary>对 <see cref="GomokuDbContext.SaveChangesAsync(CancellationToken)"/> 的薄封装。</summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly GomokuDbContext _db;

    /// <inheritdoc />
    public UnitOfWork(GomokuDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        _db.SaveChangesAsync(cancellationToken);
}
