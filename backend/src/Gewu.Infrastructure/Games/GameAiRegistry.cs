using Gewu.Domain.Ai;

namespace Gewu.Infrastructure.Games;

/// <summary>
/// 按 <c>GameKey</c> 解析 <see cref="IGameAiFactory"/>,实现由 DI 注入的集合提供。
/// <para>
/// 与 <see cref="GameRulesRegistry"/> 逐行同构 —— 这是平台上第四次出现这个形状
/// (<c>IPuzzleRulesRegistry</c> / <c>IGameRulesRegistry</c> / 前端 <c>GameCatalogService</c>
/// / 本类)。重复的是形状而不是逻辑,而形状一致的价值在于:读过任何一个,就读懂了其余三个。
/// </para>
/// <para>
/// 新增一个棋种的 AI = 一个 <see cref="IGameAiFactory"/> 实现 + 一处 <c>AddSingleton</c>。
/// 本类不用改。
/// </para>
/// </summary>
public sealed class GameAiRegistry : IGameAiRegistry
{
    private readonly Dictionary<string, IGameAiFactory> _byKey;

    /// <inheritdoc />
    public GameAiRegistry(IEnumerable<IGameAiFactory> factories)
        => _byKey = factories.ToDictionary(f => f.GameKey, StringComparer.Ordinal);

    /// <inheritdoc />
    public IGameAiFactory? For(string gameKey)
        => _byKey.TryGetValue(gameKey, out var factory) ? factory : null;
}
