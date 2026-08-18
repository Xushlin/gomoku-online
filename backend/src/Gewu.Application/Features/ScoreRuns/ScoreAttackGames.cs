using Gewu.Domain.Games.Tetris;

namespace Gewu.Application.Features.ScoreRuns;

/// <summary>
/// 计分类游戏的**唯一**判定处:哪些键能开 run、以及一串放置怎么重放。
/// <para>
/// 这**不是**注册表,而是一个只有一条分支的 switch —— 计分类只有一款游戏。本变更刻意不造
/// <c>IScoreAttackRules</c>:那会在只有一个实现时猜通用形状,而这个仓库为同一个赌注付过账
/// (<c>add-puzzle-core</c> 造了 <c>IPuzzleRules</c> 加一个形状像成语纵横的假实现来"证明"接缝通用,
/// 华容道一来两个方法都得改)。第二款计分游戏出现那天,内核从**两个真实现**之间长出来。
/// </para>
/// <para>
/// 关键是"能开 run"与"能重放"读的是**同一个事实**。两处各写一份判断,就会出现
/// <c>enforce-ai-availability</c> 那种局面:端点接受了一个后台永远处理不了的状态。
/// 那次的修法是让校验去读 <c>IGameAiRegistry</c> 而不是新加一个布尔字段;这里是同一条纪律的
/// 最小形态 —— <see cref="IsScoreAttackGame"/> 与 <see cref="Replay"/> 认的是同一个键。
/// </para>
/// </summary>
public static class ScoreAttackGames
{
    /// <summary>这个键是不是一款计分类游戏。</summary>
    /// <param name="gameKey">游戏键。</param>
    public static bool IsScoreAttackGame(string? gameKey) => gameKey == TetrisRules.GameKey;

    /// <summary>
    /// 重放一局,算出得分 / 消行 / 等级。
    /// </summary>
    /// <param name="gameKey">游戏键。</param>
    /// <param name="seed">开局时落库的种子。</param>
    /// <param name="placements">客户端提交的放置序列。</param>
    /// <exception cref="ArgumentOutOfRangeException">键不是计分类游戏 —— 调用前应先问 <see cref="IsScoreAttackGame"/>。</exception>
    /// <exception cref="Gewu.Domain.Exceptions.InvalidMoveException">任一放置不合法。</exception>
    public static (int Score, int Lines, int Level) Replay(
        string gameKey, int seed, IReadOnlyList<TetrisPlacement> placements)
    {
        if (gameKey != TetrisRules.GameKey)
        {
            throw new ArgumentOutOfRangeException(
                nameof(gameKey), gameKey, "Not a score-attack game.");
        }

        var outcome = TetrisRules.Replay(seed, placements);
        return (outcome.Score, outcome.Lines, outcome.Level);
    }
}
