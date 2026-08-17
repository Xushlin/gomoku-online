using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Tetris;

namespace Gewu.Domain.Tests.Games.Tetris;

/// <summary>
/// 重放、生成器与计分。
/// <para>
/// 这个游戏的全部就是分数,所以这些用例钉的不是"能不能玩",而是**同一串放置永远得同一分**,
/// 以及**分数只能由放置决定**。
/// </para>
/// </summary>
public class TetrisRulesTests
{
    private const int Seed = 20260817;

    // ── 生成器 ───────────────────────────────────────────────────────────────

    [Fact]
    public void The_same_seed_yields_the_same_sequence()
    {
        // 客户端与服务端各跑一份,靠的就是这条。
        TetrisPieceSequence.Take(Seed, 70)
            .Should().Equal(TetrisPieceSequence.Take(Seed, 70));
    }

    [Fact]
    public void Different_seeds_yield_different_sequences()
    {
        TetrisPieceSequence.Take(Seed, 50)
            .Should().NotEqual(TetrisPieceSequence.Take(Seed + 1, 50));
    }

    [Fact]
    public void All_seven_kinds_appear()
    {
        // 一个只发某几种的生成器会让重放全绿而游戏不可玩。
        TetrisPieceSequence.Take(Seed, 70).Distinct().Should().HaveCount(7);
    }

    [Fact]
    public void Each_bag_of_seven_is_a_permutation_of_all_seven()
    {
        // 七袋法的意义:纯随机会出现长串同种方块,而那让分数更多取决于运气。
        var seq = TetrisPieceSequence.Take(Seed, 7 * 8);
        for (var bag = 0; bag < 8; bag++)
        {
            seq.Skip(bag * 7).Take(7).Distinct().Should().HaveCount(7, $"bag {bag}");
        }
    }

    [Fact]
    public void A_zero_seed_does_not_degenerate()
    {
        // xorshift 的状态为 0 会永远停在 0 —— 那会退化成"永远第一种方块"。
        TetrisPieceSequence.Take(0, 70).Distinct().Should().HaveCount(7);
    }

    // ── 形状表 ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TetrominoKind.I)]
    [InlineData(TetrominoKind.O)]
    [InlineData(TetrominoKind.T)]
    [InlineData(TetrominoKind.S)]
    [InlineData(TetrominoKind.Z)]
    [InlineData(TetrominoKind.J)]
    [InlineData(TetrominoKind.L)]
    public void Every_rotation_of_every_piece_has_four_cells(TetrominoKind kind)
    {
        for (var r = 0; r < Tetromino.Rotations; r++)
        {
            Tetromino.CellsOf(kind, r).Should().HaveCount(4, $"{kind} rotation {r}");
        }
    }

    [Fact]
    public void The_O_piece_is_the_same_in_every_rotation()
    {
        // 方块转不动 —— 若旋转推导有误,它最先露出来。
        for (var r = 1; r < Tetromino.Rotations; r++)
        {
            Tetromino.CellsOf(TetrominoKind.O, r)
                .Should().Equal(Tetromino.CellsOf(TetrominoKind.O, 0));
        }
    }

    [Fact]
    public void The_I_piece_is_four_wide_flat_and_one_wide_upright()
    {
        Tetromino.WidthOf(TetrominoKind.I, 0).Should().Be(4);
        Tetromino.WidthOf(TetrominoKind.I, 1).Should().Be(1);
    }

    [Fact]
    public void Rotation_wraps_and_accepts_negatives()
    {
        Tetromino.CellsOf(TetrominoKind.T, 4).Should().Equal(Tetromino.CellsOf(TetrominoKind.T, 0));
        Tetromino.CellsOf(TetrominoKind.T, -1).Should().Equal(Tetromino.CellsOf(TetrominoKind.T, 3));
    }

    // ── 重放 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 生成 <paramref name="count"/> 个**一定合法**的放置:列按该方块该旋转态的宽度取模,
    /// 所以永远不越界。
    /// <para>
    /// 第一版写的是 <c>i % Columns</c>,于是宽 ≥ 2 的形状落在列 9 上越界,两条用例红了 ——
    /// 而红的是 helper,不是规则:规则**正确地**拒绝了越界。测试脚手架的 bug 与被测物的 bug
    /// 长得一样,区别只在于读栈。
    /// </para>
    /// </summary>
    private static IReadOnlyList<TetrisPlacement> LegalSweep(int seed, int count, int rotation = 0)
    {
        var pieces = TetrisPieceSequence.Take(seed, count);
        var result = new List<TetrisPlacement>(count);
        for (var i = 0; i < count; i++)
        {
            var width = Tetromino.WidthOf(pieces[i], rotation);
            result.Add(new TetrisPlacement(rotation, i % (TetrisRules.Columns - width + 1)));
        }
        return result;
    }

    [Fact]
    public void Replaying_the_same_input_twice_gives_the_same_result()
    {
        // 重放**必须**确定性,否则同一局提交两次会得两个分。
        var placements = LegalSweep(Seed, 20);

        TetrisRules.Replay(Seed, placements).Should().Be(TetrisRules.Replay(Seed, placements));
    }

    [Fact]
    public void An_empty_run_scores_nothing()
    {
        TetrisRules.Replay(Seed, []).Should().Be(new TetrisOutcome(0, 0, 1));
    }

    [Fact]
    public void A_placement_that_does_not_fit_the_field_is_refused()
    {
        // 第 0 个方块无论是什么,列 9 加上任何宽度 ≥ 2 的形状都会越界;宽 1 的只有 I 竖放。
        var kind = TetrisPieceSequence.Take(Seed, 1)[0];
        var rotation = Tetromino.WidthOf(kind, 0) > 1 ? 0 : 1;

        var act = () => TetrisRules.Replay(Seed, [new TetrisPlacement(rotation, TetrisRules.Columns - 1)]);

        act.Should().Throw<InvalidMoveException>().WithMessage("*does not fit*");
    }

    [Fact]
    public void A_negative_column_is_refused()
    {
        var act = () => TetrisRules.Replay(Seed, [new TetrisPlacement(0, -1)]);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Stacking_past_the_top_is_refused_rather_than_silently_skipped()
    {
        // 场地 20 行,每个方块至少占 1 行 —— 往同一列堆 40 个必然顶穿。
        // 拒绝整局而不是跳过那一步:跳过等于替客户端决定"这一步不算",而那是它自己的分数。
        var sameColumn = Enumerable.Range(0, 40).Select(_ => new TetrisPlacement(1, 0)).ToList();

        var act = () => TetrisRules.Replay(Seed, sameColumn);

        act.Should().Throw<InvalidMoveException>().WithMessage("*too high*");
    }

    // ── 计分 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Level_starts_at_one_and_rises_every_ten_lines()
    {
        TetrisRules.LevelFor(0).Should().Be(1);
        TetrisRules.LevelFor(9).Should().Be(1);
        TetrisRules.LevelFor(10).Should().Be(2);
        TetrisRules.LevelFor(29).Should().Be(3);
        TetrisRules.LevelFor(30).Should().Be(4);
    }

    [Fact]
    public void Four_lines_at_once_beats_four_single_lines()
    {
        // 800 对 4×100。若四行等于四倍单行,"攒四行"这个核心决策就消失了。
        TetrisRules.ScoreForClear(4, 0).Should().Be(800);
        TetrisRules.ScoreForClear(1, 0).Should().Be(100);
        TetrisRules.ScoreForClear(4, 0).Should().BeGreaterThan(4 * TetrisRules.ScoreForClear(1, 0));
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 300)]
    [InlineData(3, 500)]
    [InlineData(4, 800)]
    public void The_base_score_per_cleared_line_count_is_standard(int cleared, int expected)
    {
        // 公式是对外契约的一部分 —— 一个非标准的公式会让所有分数无从比较。
        TetrisRules.ScoreForClear(cleared, 0).Should().Be(expected);
    }

    [Fact]
    public void Score_scales_with_the_level_the_lines_were_cleared_at()
    {
        // 这两条此前是纯常量算术,**不碰实现** —— 把等级因子改成 1,全部 25 条依然绿。
        // 变异测试当场证伪了我为此写的理由("构造消四行需要求解器")。
        TetrisRules.ScoreForClear(1, 0).Should().Be(100);    // 等级 1
        TetrisRules.ScoreForClear(1, 10).Should().Be(200);   // 等级 2
        TetrisRules.ScoreForClear(1, 20).Should().Be(300);   // 等级 3
    }

    [Fact]
    public void The_level_used_is_the_one_before_the_clear_not_after()
    {
        // 消到第 10 行的那一手,按等级 1 算而不是 2 —— 否则跨级那一手会凭空多拿一档。
        TetrisRules.ScoreForClear(1, 9).Should().Be(100);
        TetrisRules.ScoreForClear(1, 10).Should().Be(200);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void A_clear_count_outside_one_to_four_is_refused(int cleared)
    {
        var act = () => TetrisRules.ScoreForClear(cleared, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// 最低优先贪心:每个方块试遍所有 (旋转, 列),选落点最低的那个。
    /// <para>
    /// 它不求最优,只要"能真的消到行"。之所以能写在测试里,是因为 <see cref="TetrisField"/>
    /// 是公开 API —— 而它公开的真正理由是客户端要画硬降预览。
    /// </para>
    /// </summary>
    private static (IReadOnlyList<TetrisPlacement> Placements, int Cleared) GreedySweep(int seed, int pieces)
    {
        var kinds = TetrisPieceSequence.Take(seed, pieces);
        var field = new TetrisField();
        var placements = new List<TetrisPlacement>(pieces);
        var cleared = 0;

        foreach (var kind in kinds)
        {
            var best = (Rotation: -1, Column: -1, Landing: -1);
            for (var rot = 0; rot < Tetromino.Rotations; rot++)
            {
                var width = Tetromino.WidthOf(kind, rot);
                for (var col = 0; col + width <= TetrisRules.Columns; col++)
                {
                    var landing = field.LandingRow(kind, rot, col);
                    if (landing is int row && row > best.Landing)
                    {
                        best = (rot, col, row);
                    }
                }
            }

            if (best.Rotation < 0) break;   // 堆满了,停手
            cleared += field.PlaceAndClear(kind, best.Rotation, best.Column);
            placements.Add(new TetrisPlacement(best.Rotation, best.Column));
        }

        return (placements, cleared);
    }

    [Fact]
    public void A_greedy_run_really_clears_lines_and_scores_them()
    {
        // 前几版这条用的是"按宽度取模铺一遍",结果 17 手就堆满 —— 那验的是脚手架不是规则。
        // 换成贪心之后它真的消到行,而断言仍然只针对规则:消行数为正、分数为正、等级由行数决定。
        var (placements, clearedWhileBuilding) = GreedySweep(Seed, 200);
        clearedWhileBuilding.Should().BePositive("贪心必须真的消到行,否则这条什么都没验");

        var outcome = TetrisRules.Replay(Seed, placements);

        outcome.Lines.Should().Be(clearedWhileBuilding, "重放得到的行数必须与构造时一致");
        outcome.Score.Should().BePositive();
        outcome.Level.Should().Be(TetrisRules.LevelFor(outcome.Lines));
    }

    [Fact]
    public void The_field_and_the_replay_agree_on_everything()
    {
        // TetrisField 是公开 API(客户端要用),Replay 内部也用它。这条钉住"同一串放置,
        // 逐手用 field 走一遍与整局 Replay 一遍,结果相同" —— 两条路 MUST NOT 分叉。
        var (placements, cleared) = GreedySweep(Seed + 7, 150);

        TetrisRules.Replay(Seed + 7, placements).Lines.Should().Be(cleared);
    }

}
