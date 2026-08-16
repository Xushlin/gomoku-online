using Gewu.Domain.Games.NInARow;
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
        // 落子类 AI 用 Board 思考,而外层接缝收历史、给 MoveIntent —— 适配器补这一段,
        // 于是那五个实现连同它们的测试一行不用改。
        IPlacementAi inner = difficulty switch
        {
            BotDifficulty.Easy => new EasyAi(random),
            BotDifficulty.Medium => new MediumAi(random),
            BotDifficulty.Hard => new HardAi(random),
            _ => throw new ArgumentOutOfRangeException(
                nameof(difficulty), difficulty, "Unknown BotDifficulty value."),
        };
        return new PlacementAiAdapter(inner, BuiltInGameRules.Gomoku);
    }
}
