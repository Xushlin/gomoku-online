namespace Gewu.Domain.Puzzles;

/// <summary>
/// 一个单人关卡。<c>(GameKey, LevelIndex)</c> 全库唯一。
/// <para>
/// ⚠️ <see cref="SolutionJson"/> **永不下发客户端**。它和 <see cref="LayoutJson"/> 分成两列
/// 而不是合成一个 payload,是为了让"泄漏答案"表现为**有人新增了一个 DTO 属性**,
/// 而不是"有人忘了删一个属性" —— 把约束做成结构性的,而不是纪律性的。
/// </para>
/// <para>
/// 两列都是对平台不透明的字符串:三个关卡类游戏的关卡形状差别太大(字格 / 滑块 / 释义),
/// 平台不理解内容,只保证答案的去向。
/// </para>
/// </summary>
public sealed class PuzzleLevel
{
    /// <summary>自增主键。</summary>
    public int Id { get; private set; }

    /// <summary>所属游戏键。</summary>
    public string GameKey { get; private set; } = string.Empty;

    /// <summary>关卡序号,0 起。同一游戏内唯一,也决定解锁顺序。</summary>
    public int LevelIndex { get; private set; }

    /// <summary>难度分档,供客户端分组展示;语义由游戏自定。</summary>
    public int Difficulty { get; private set; }

    /// <summary>关卡布局。**会**下发客户端。</summary>
    public string LayoutJson { get; private set; } = string.Empty;

    /// <summary>关卡答案。**永不**下发客户端 —— 校验、提示、计分都在服务端对它执行。</summary>
    public string SolutionJson { get; private set; } = string.Empty;

    // EF 物化用。
    private PuzzleLevel() { }

    /// <summary>创建一个关卡。</summary>
    /// <param name="gameKey">游戏键,非空。</param>
    /// <param name="levelIndex">关卡序号,不得为负。</param>
    /// <param name="difficulty">难度分档。</param>
    /// <param name="layoutJson">布局,非空。</param>
    /// <param name="solutionJson">答案,非空。</param>
    /// <exception cref="ArgumentException">游戏键、布局或答案为空。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="levelIndex"/> 为负。</exception>
    public static PuzzleLevel Create(
        string gameKey,
        int levelIndex,
        int difficulty,
        string layoutJson,
        string solutionJson)
    {
        if (string.IsNullOrWhiteSpace(gameKey))
        {
            throw new ArgumentException("Game key must be non-empty.", nameof(gameKey));
        }
        if (levelIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(levelIndex), levelIndex, "Level index must not be negative.");
        }
        if (string.IsNullOrWhiteSpace(layoutJson))
        {
            throw new ArgumentException("Layout must be non-empty.", nameof(layoutJson));
        }
        if (string.IsNullOrWhiteSpace(solutionJson))
        {
            throw new ArgumentException("Solution must be non-empty.", nameof(solutionJson));
        }

        return new PuzzleLevel
        {
            GameKey = gameKey.Trim(),
            LevelIndex = levelIndex,
            Difficulty = difficulty,
            LayoutJson = layoutJson,
            SolutionJson = solutionJson,
        };
    }
}
