using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;

namespace Gewu.Application.Tests;

/// <summary>
/// 测试用的规则注册表 —— 只登记五子棋,就是 <c>DependencyInjection</c> 在生产里注册的
/// 同一份 <see cref="BuiltInGameRules.Gomoku"/> 实例。
/// <para>
/// 手写一个 mock 注册表也行,但那样测的就是"handler 会调注册表",而不是"handler 用
/// 五子棋的规则判子"。用真规则,测试才在断言真行为。
/// </para>
/// </summary>
internal static class GomokuRules
{
    /// <summary>只含五子棋的注册表。</summary>
    internal static readonly IGameRulesRegistry Registry = new SingleGameRegistry();

    private sealed class SingleGameRegistry : IGameRulesRegistry
    {
        public IGameRules? For(string gameKey)
            => gameKey == BuiltInGameRules.Gomoku.GameKey ? BuiltInGameRules.Gomoku : null;
    }
}
