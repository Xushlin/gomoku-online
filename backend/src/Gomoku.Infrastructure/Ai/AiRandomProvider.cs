using Gomoku.Application.Abstractions;

namespace Gomoku.Infrastructure.Ai;

/// <summary>
/// <see cref="IAiRandomProvider"/> 的生产实现。返回 <see cref="Random.Shared"/> —— 线程安全,
/// 由 .NET 运行时自动处理每线程实例。单例注册即可,无需每次请求构造新 <see cref="Random"/>。
/// </summary>
public sealed class AiRandomProvider : IAiRandomProvider
{
    /// <inheritdoc />
    public Random Get() => Random.Shared;
}
