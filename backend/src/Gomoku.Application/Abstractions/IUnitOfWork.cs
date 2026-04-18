namespace Gomoku.Application.Abstractions;

/// <summary>
/// 持久化保存点。Handler 在内存中修改聚合后,通过此接口提交一次变更。
/// 本次设计刻意不跨 handler 做事务 —— Handler 自行决定何时 <see cref="SaveChangesAsync"/>。
/// </summary>
public interface IUnitOfWork
{
    /// <summary>把当前跟踪的所有变更提交到存储。返回受影响行数。</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
