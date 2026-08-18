using Gewu.Application.Abstractions;

namespace Gewu.Infrastructure.Common;

/// <summary><see cref="ISeedProvider"/> 的生产实现 —— <see cref="Random.Shared"/>。</summary>
public sealed class SystemSeedProvider : ISeedProvider
{
    /// <inheritdoc />
    public int NextSeed() => Random.Shared.Next();
}
