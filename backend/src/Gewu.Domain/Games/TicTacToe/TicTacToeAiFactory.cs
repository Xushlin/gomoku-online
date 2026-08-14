using Gewu.Domain.Ai;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Domain.Games.TicTacToe;

/// <summary>
/// 一字棋的 AI 工厂。无状态,每次调用返回新实例。
/// <para>
/// <see cref="BotDifficulty.Easy"/> **复用五子棋的 <see cref="EasyAi"/>,一行都没改** ——
/// 它只按 <c>board.Rows</c> / <c>board.Cols</c> 遍历空格再均匀随机,不含任何棋种假设。
/// 这是 <c>add-game-rules-registry</c> 那次泛化到目前为止唯一的实证回报,值得写明:
/// 另外两档就是它的反证,各自都得重写。
/// </para>
/// </summary>
public sealed class TicTacToeAiFactory : IGameAiFactory
{
    /// <inheritdoc />
    public string GameKey => GameKeys.TicTacToe;

    /// <inheritdoc />
    public IBoardGameAi Create(BotDifficulty difficulty, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return difficulty switch
        {
            BotDifficulty.Easy => new EasyAi(random),
            BotDifficulty.Medium => new TicTacToeMediumAi(random),
            // 穷举搜索,不需要随机源 —— 确定性让"永不落败"可以穷举验证。
            BotDifficulty.Hard => new TicTacToeHardAi(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(difficulty), difficulty, "Unknown BotDifficulty value."),
        };
    }
}
