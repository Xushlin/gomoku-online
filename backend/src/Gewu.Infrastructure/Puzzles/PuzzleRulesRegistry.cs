using Gewu.Domain.Puzzles;

namespace Gewu.Infrastructure.Puzzles;

/// <summary>
/// 按 <c>GameKey</c> 解析 <see cref="IPuzzleRules"/>,实现由 DI 注入的集合提供。
/// <para>
/// 新增一个关卡类游戏 = 一个 <see cref="IPuzzleRules"/> 实现 + 一处
/// <c>services.AddSingleton&lt;IPuzzleRules, XxxRules&gt;()</c>,本类不用改。
/// </para>
/// <para>
/// puzzle-core 本身**不注册任何游戏**,所以在 成语纵横 落地前它一律返回 <c>null</c>,
/// handler 把它映射成 404 —— 这正是"这个游戏在本平台不存在"的诚实答复。
/// </para>
/// </summary>
public sealed class PuzzleRulesRegistry : IPuzzleRulesRegistry
{
    private readonly Dictionary<string, IPuzzleRules> _byKey;

    /// <inheritdoc />
    public PuzzleRulesRegistry(IEnumerable<IPuzzleRules> rules)
        => _byKey = rules.ToDictionary(r => r.GameKey, StringComparer.Ordinal);

    /// <inheritdoc />
    public IPuzzleRules? For(string gameKey)
        => _byKey.TryGetValue(gameKey, out var rules) ? rules : null;
}
