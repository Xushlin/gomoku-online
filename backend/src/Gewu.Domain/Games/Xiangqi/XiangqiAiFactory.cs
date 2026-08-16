using Gewu.Domain.Ai;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Domain.Games.Xiangqi;

/// <summary>
/// 中国象棋的 AI 工厂。三档难度只有一个参数不同：搜索深度。
/// <para>
/// 与五子棋 / 一字棋不同，这里**没有一档是穷举的** —— 象棋的状态空间不允许。
/// 所以三档之间是「看得远一点」的差别，不是「近似 vs 完美」的差别，
/// 也因此本工厂不声称任何一档不可战胜。
/// </para>
/// </summary>
public sealed class XiangqiAiFactory : IGameAiFactory
{
    private static readonly XiangqiRules Rules = new();

    /// <inheritdoc />
    public string GameKey => GameKeys.Xiangqi;

    /// <inheritdoc />
    public IBoardGameAi Create(BotDifficulty difficulty, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var depth = difficulty switch
        {
            // 深度 0：只比较「这一步吃到什么」，不看对手的回应。会送子，但从不走非法着法。
            BotDifficulty.Easy => 0,
            // 深度 2：看一个回合往返，因此不会白送子给一步就能吃回来的对手。
            BotDifficulty.Medium => 2,
            // 深度 3：能看到「吃子—反吃—再吃」这条链。再深就明显变慢，
            // 而在没有 UI、没人体感的今天，多一层只是更慢。
            BotDifficulty.Hard => 3,
            _ => throw new ArgumentOutOfRangeException(
                nameof(difficulty), difficulty, "Unknown BotDifficulty value."),
        };
        return new XiangqiAi(Rules, random, depth);
    }
}
