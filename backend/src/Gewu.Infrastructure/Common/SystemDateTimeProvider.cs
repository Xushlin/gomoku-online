using Gewu.Application.Abstractions;

namespace Gewu.Infrastructure.Common;

/// <summary>返回 <see cref="DateTime.UtcNow"/> 的默认 <see cref="IDateTimeProvider"/> 实现。</summary>
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}
