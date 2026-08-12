namespace Gomoku.Application.Abstractions;

/// <summary>
/// 全系统"现在"(UTC)的唯一来源。所有 Domain / Application 层需要时间戳的地方
/// 都必须通过此抽象读取,便于测试注入固定时间、并保持 Domain 层零外部依赖。
/// Infrastructure 层提供默认实现 <c>SystemDateTimeProvider</c> 返回 <c>DateTime.UtcNow</c>。
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>当前 UTC 时间。</summary>
    DateTime UtcNow { get; }
}
