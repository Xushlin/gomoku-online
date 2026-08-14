using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;

namespace Gewu.Application.Tests;

/// <summary>
/// 测试用的规则注册表 —— 登记的是 <c>DependencyInjection</c> 在生产里注册的**同一批**
/// <see cref="BuiltInGameRules"/> 实例。
/// <para>
/// 手写一个 mock 注册表也行,但那样测的就是"handler 会调注册表",而不是"handler 用
/// 那个棋种的规则判子"。用真规则,测试才在断言真行为。
/// </para>
/// </summary>
internal static class GomokuRules
{
    /// <summary>
    /// 与生产 DI 一致的注册表:五子棋 + 一字棋。
    /// <para>
    /// 名字里的 "Gomoku" 是历史包袱 —— 这个 helper 现在服务全部内置棋种。
    /// 留着不改是因为它只被测试引用,改名会让本变更的 diff 里多出一堆与一字棋无关的噪声,
    /// 而本变更的 diff 大小本身就是要被拿来读的数据(见 tasks §7)。
    /// </para>
    /// </summary>
    internal static readonly IGameRulesRegistry Registry =
        new StaticRegistry(BuiltInGameRules.Gomoku, BuiltInGameRules.TicTacToe);

    /// <summary>
    /// 只登记五子棋的注册表 —— 给那些需要"解析不出来"这条路径的测试用
    /// (房间指向本构建不认识的棋种 → 404)。
    /// </summary>
    internal static readonly IGameRulesRegistry GomokuOnly =
        new StaticRegistry(BuiltInGameRules.Gomoku);

    private sealed class StaticRegistry(params IGameRules[] rules) : IGameRulesRegistry
    {
        private readonly Dictionary<string, IGameRules> _byKey =
            rules.ToDictionary(r => r.GameKey, StringComparer.Ordinal);

        public IGameRules? For(string gameKey)
            => _byKey.TryGetValue(gameKey, out var found) ? found : null;
    }
}
