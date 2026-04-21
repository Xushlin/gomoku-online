namespace Gomoku.Domain.Ai;

/// <summary>
/// 按 <see cref="BotDifficulty"/> 构造 AI 实现。工厂无状态,每次调用返回新实例。
/// 新增 <c>Hard</c> 只需在此追加一个 case。
/// </summary>
public static class GomokuAiFactory
{
    /// <summary>
    /// 构造指定难度的 AI 实例。
    /// </summary>
    /// <param name="difficulty">AI 难度。</param>
    /// <param name="random">随机源,交由具体实现用于初始选点 / 并列打破。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="difficulty"/> 不是已定义值。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> 为 <c>null</c>。</exception>
    public static IGomokuAi Create(BotDifficulty difficulty, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return difficulty switch
        {
            BotDifficulty.Easy => new EasyAi(random),
            BotDifficulty.Medium => new MediumAi(random),
            BotDifficulty.Hard => new HardAi(random),
            _ => throw new ArgumentOutOfRangeException(
                nameof(difficulty), difficulty, "Unknown BotDifficulty value."),
        };
    }
}
