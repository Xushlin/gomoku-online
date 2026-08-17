using Gewu.Domain.Exceptions;

namespace Gewu.Domain.Games.Tetris;

/// <summary>一次放置:第几个方块、什么旋转态、最左格落在哪一列。</summary>
/// <param name="Rotation">旋转态 0–3。</param>
/// <param name="Column">该旋转态最左格所在的列。</param>
public readonly record struct TetrisPlacement(int Rotation, int Column);

/// <summary>重放一局的结果。</summary>
/// <param name="Score">得分。</param>
/// <param name="Lines">累计消行数。</param>
/// <param name="Level">结束时的等级。</param>
public readonly record struct TetrisOutcome(int Score, int Lines, int Level);

/// <summary>
/// 俄罗斯方块的规则与重放。
/// <para>
/// **重放的粒度是放置,不是按键。** 提交完整输入流会要求客户端与服务端两份模拟逐帧一致
/// (重力间隔、锁定延迟、软降速率、等级曲线),而任何一处差一帧,合法的一局就会被拒 ——
/// 玩家看到的是「我明明打完了,它说我作弊」。这与 <c>add-web-xiangqi</c> 拒绝把象棋规则
/// 港到 TypeScript 是同一条理由:两个必须位对位一致的真源。
/// </para>
/// <para>
/// 放置这个粒度不涉及任何计时,而且每一步可判定 —— 形状在那个列合法吗、落下停在哪、消了几行。
/// 它与华容道「一次滑动」同级,而那个模型已被一款真游戏验过。
/// </para>
/// <para>
/// <b>诚实的限制</b>:重放保证的是**分数与放置一致**,不是**放置出自人手**。离线求解器可以按
/// 服务端给的种子算出接近最优的下法。这一条不假装被解决了 —— 一个无法验证的断言比没有断言更糟
/// (<c>add-xiangqi-ai</c> 立下的规矩)。重放的价值是把"随便报个数"降级成"你得真算出一个
/// 能拿那个分的下法"。
/// </para>
/// </summary>
public static class TetrisRules
{
    /// <summary>场地列数。</summary>
    public const int Columns = 10;

    /// <summary>场地行数。</summary>
    public const int Rows = 20;

    /// <summary>每升一级需要的消行数。</summary>
    public const int LinesPerLevel = 10;

    /// <summary>消 1/2/3/4 行的基础分。四行 MUST NOT 等于四倍单行 —— 那会让"攒四行"这个核心决策消失。</summary>
    private static readonly int[] LineScores = [0, 100, 300, 500, 800];

    /// <summary>
    /// 重放一整局。
    /// </summary>
    /// <param name="seed">服务端下发的种子 —— 方块序列由它决定。</param>
    /// <param name="placements">按顺序的放置。第 i 项对应序列里第 i 个方块。</param>
    /// <exception cref="InvalidMoveException">任一放置不合法,或场地已满放不下。</exception>
    public static TetrisOutcome Replay(int seed, IReadOnlyList<TetrisPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);

        var pieces = TetrisPieceSequence.Take(seed, placements.Count);
        var field = new TetrisField();
        var score = 0;
        var lines = 0;

        for (var i = 0; i < placements.Count; i++)
        {
            var kind = pieces[i];
            var (rotation, column) = (placements[i].Rotation, placements[i].Column);

            int cleared;
            try
            {
                // 场地拒绝时把序号补进消息 —— 只说"放不下"的错误,调试时要靠数手指找是第几手。
                cleared = field.PlaceAndClear(kind, rotation, column);
            }
            catch (InvalidMoveException ex)
            {
                throw new InvalidMoveException($"Placement {i}: {ex.Message}", ex);
            }

            if (cleared > 0)
            {
                lines += cleared;
                // 等级用**消行之前**的那个 —— 消这几行时玩家处在哪一级,分就按哪一级算。
                score += ScoreForClear(cleared, lines - cleared);
            }
        }

        return new TetrisOutcome(score, lines, LevelFor(lines));
    }

    /// <summary>等级:每 <see cref="LinesPerLevel"/> 行升一级,从 1 起。</summary>
    /// <param name="lines">累计消行数。</param>
    public static int LevelFor(int lines) => lines / LinesPerLevel + 1;

    /// <summary>
    /// 一次消行得多少分。
    /// <para>
    /// 它是**公开的**,因为这条公式是这个游戏对外契约的一部分(分数榜要能被理解),
    /// 而且客户端要显示"这一手得了多少分"。
    /// </para>
    /// <para>
    /// 它此前埋在 <see cref="Replay"/> 的循环里,于是等级放大这件事**没有任何测试守着** ——
    /// 我写的那两条"计分"用例都只是常量算术,不碰实现。变异测试当场证伪了:把等级因子改成 1,
    /// 25 条全绿。**一条断言只测到它真正调用的东西。**
    /// </para>
    /// </summary>
    /// <param name="clearedLines">这一手消了几行,1–4。</param>
    /// <param name="linesBefore">这一手之前的累计消行数 —— 等级按它算。</param>
    /// <exception cref="ArgumentOutOfRangeException">消行数不在 1–4。</exception>
    public static int ScoreForClear(int clearedLines, int linesBefore)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(clearedLines, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(clearedLines, 4);
        return LineScores[clearedLines] * LevelFor(linesBefore);
    }


}
