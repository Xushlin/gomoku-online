using Gewu.Application.Common.Exceptions;
using Gewu.Domain.Puzzles;

namespace Gewu.Application.Features.Puzzles;

/// <summary>
/// 把"未注册的游戏键"统一翻译成 404。
/// <para>
/// 每个 handler 都要做这一步,抽出来是为了让"未知游戏 = 404,而不是 500 也不是 400"
/// 这条决定只存在一处。本变更不注册任何游戏,所以在 成语纵横 落地前所有路由都会走到这里。
/// </para>
/// </summary>
internal static class PuzzleRulesResolver
{
    /// <summary>解析规则,未注册则抛 <see cref="PuzzleNotFoundException"/>。</summary>
    internal static IPuzzleRules Resolve(IPuzzleRulesRegistry registry, string gameKey)
        => registry.For(gameKey)
           ?? throw new PuzzleNotFoundException($"No puzzle game is registered for key '{gameKey}'.");
}
