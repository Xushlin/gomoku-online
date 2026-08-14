using Gewu.Domain.Games.Abstractions;

namespace Gewu.Domain.Ai;

/// <summary>
/// 五子棋的 AI 工厂。工厂无状态,每次调用返回新实例。
/// <para>
/// 由静态类改为 <see cref="IGameAiFactory"/> 实现 —— 静态类没有键,注册表也就无从按棋种
/// 解析它。三个难度分支一字未改。
/// </para>
/// </summary>
public sealed class GomokuAiFactory : IGameAiFactory
{
    /// <inheritdoc />
    public string GameKey => GameKeys.Gomoku;

    /// <inheritdoc />
    public IBoardGameAi Create(BotDifficulty difficulty, Random random)
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
