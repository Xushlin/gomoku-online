using Gewu.Domain.Games.Abstractions;

namespace Gewu.Infrastructure.Games;

/// <summary>
/// 按 <c>GameKey</c> 解析 <see cref="IGameRules"/>,实现由 DI 注入的集合提供。
/// <para>
/// 新增一个棋盘对抗游戏 = 一个 <see cref="IGameRules"/>(连 N 子类棋种直接复用
/// <c>NInARowRules</c>,连类都不用写)+ 一处 <c>AddSingleton</c>。本类不用改。
/// </para>
/// <para>
/// 形状与 <c>PuzzleRulesRegistry</c> 完全一致 —— 平台上"按游戏键解析实现"只该有一种写法。
/// </para>
/// </summary>
public sealed class GameRulesRegistry : IGameRulesRegistry
{
    private readonly Dictionary<string, IGameRules> _byKey;

    /// <inheritdoc />
    public GameRulesRegistry(IEnumerable<IGameRules> rules)
        => _byKey = rules.ToDictionary(r => r.GameKey, StringComparer.Ordinal);

    /// <inheritdoc />
    public IGameRules? For(string gameKey)
        => _byKey.TryGetValue(gameKey, out var rules) ? rules : null;

    /// <inheritdoc />
    public IReadOnlyCollection<IGameRules> All => _byKey.Values;
}
