using Gewu.Domain.Ai;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.TicTacToe;

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
    /// 与生产 DI 一致的注册表 —— 取自 <see cref="BuiltInGameRules.All"/>,与
    /// <c>DependencyInjection</c> **同一份**清单。
    /// <para>
    /// 它此前手写成 <c>{ Gomoku, TicTacToe }</c>,注释却写着"与生产 DI 一致"。象棋自
    /// <c>add-xiangqi</c> 起就在生产注册表里,于是本项目的每一条按键解析规则的测试,都在
    /// 一个没有象棋的世界里跑,而注释让人相信不是这样。这正是 <c>add-xiangqi</c> 已经
    /// 修过一次的那个缺陷 —— 它建了 <c>All</c> 这份唯一清单,却没回头把这个夹具接上去。
    /// </para>
    /// <para>
    /// 名字里的 "Gomoku" 是历史包袱 —— 这个 helper 服务全部内置棋种。
    /// </para>
    /// </summary>
    internal static readonly IGameRulesRegistry Registry =
        new StaticRegistry([.. BuiltInGameRules.All]);

    /// <summary>
    /// 只登记五子棋的注册表 —— 给那些需要"解析不出来"这条路径的测试用
    /// (房间指向本构建不认识的棋种 → 404)。
    /// <para>
    /// 这一个**故意**是手写的残缺清单,那正是它的用途;上面那个不是。
    /// </para>
    /// </summary>
    internal static readonly IGameRulesRegistry GomokuOnly =
        new StaticRegistry(BuiltInGameRules.Gomoku);

    /// <summary>与生产 DI 一致的 AI 注册表:五子棋 + 一字棋,用的是真工厂。</summary>
    internal static readonly IGameAiRegistry AiRegistry =
        new StaticAiRegistry(new GomokuAiFactory(), new TicTacToeAiFactory());

    /// <summary>只登记五子棋 AI 的注册表 —— 给"这个棋种没有 AI"那条 404 路径用。</summary>
    internal static readonly IGameAiRegistry GomokuAiOnly =
        new StaticAiRegistry(new GomokuAiFactory());

    private sealed class StaticRegistry(params IGameRules[] rules) : IGameRulesRegistry
    {
        private readonly Dictionary<string, IGameRules> _byKey =
            rules.ToDictionary(r => r.GameKey, StringComparer.Ordinal);

        public IGameRules? For(string gameKey)
            => _byKey.TryGetValue(gameKey, out var found) ? found : null;

        public IReadOnlyCollection<IGameRules> All => _byKey.Values;
    }

    private sealed class StaticAiRegistry(params IGameAiFactory[] factories) : IGameAiRegistry
    {
        private readonly Dictionary<string, IGameAiFactory> _byKey =
            factories.ToDictionary(f => f.GameKey, StringComparer.Ordinal);

        public IGameAiFactory? For(string gameKey)
            => _byKey.TryGetValue(gameKey, out var found) ? found : null;
    }
}
