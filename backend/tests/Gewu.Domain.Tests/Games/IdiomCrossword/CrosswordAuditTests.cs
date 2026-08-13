using Gewu.Domain.Games.IdiomCrossword;

namespace Gewu.Domain.Tests.Games.IdiomCrossword;

/// <summary>
/// 审计必须能抓住每一种坏关卡。
/// <para>
/// 生成器在摆放时已经校验过一遍,审计是**刻意的冗余** —— 它守的那些性质一旦被破坏,
/// 产出的是"能解但读起来是乱码"或"根本无解"的谜题,而这种坏法不会让任何别的测试自然失败。
/// 所以这里给每一条审计规则都递一个专门做坏的关卡。
/// </para>
/// </summary>
public class CrosswordAuditTests
{
    private const string Across = "合而为一";
    private const string Down = "合情合理";

    private static ISet<string> Dictionary()
        => new HashSet<string>(StringComparer.Ordinal) { Across, Down, "情理之中" };

    /// <summary>一个健康的两词关卡:横竖共用 (0,0) 的「合」,预填该格。</summary>
    private static GeneratedLevel HealthyLevel()
    {
        var cells = new List<CrosswordCell>();
        var solutionCells = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < Across.Length; i++)
        {
            cells.Add(new CrosswordCell(0, i));
            solutionCells[CrosswordSolution.Key(0, i)] = Across[i].ToString();
        }
        for (var i = 1; i < Down.Length; i++)
        {
            cells.Add(new CrosswordCell(i, 0));
            solutionCells[CrosswordSolution.Key(i, 0)] = Down[i].ToString();
        }

        var layout = new CrosswordLayout(
            Rows: 4,
            Cols: 4,
            Cells: cells,
            Given: new[] { new CrosswordGivenCell(0, 0, "合") },
            Tray: new[] { "而", "为", "一", "情", "合", "理" },
            Slots: new[]
            {
                new CrosswordSlot(0, 0, 0, CrosswordDirection.Horizontal, 4),
                new CrosswordSlot(1, 0, 0, CrosswordDirection.Vertical, 4),
            });

        var solution = new CrosswordSolution(solutionCells, new[]
        {
            new CrosswordSolvedWord(0, Across, "合成一个整体。"),
            new CrosswordSolvedWord(1, Down, "合乎情理。"),
        });

        return new GeneratedLevel(layout, solution, Difficulty: 1);
    }

    [Fact]
    public void A_healthy_level_passes()
    {
        var audit = CrosswordAudit.Check(HealthyLevel(), Dictionary());

        audit.Failures.Should().BeEmpty();
        audit.Passed.Should().BeTrue();
    }

    [Fact]
    public void A_tray_missing_a_needed_character_is_rejected()
    {
        var level = HealthyLevel();
        // 抽掉「理」—— 关卡从此无解。
        var broken = level with
        {
            Layout = level.Layout with
            {
                Tray = level.Layout.Tray.Where(t => t != "理").ToList(),
            },
        };

        var audit = CrosswordAudit.Check(broken, Dictionary());

        audit.Passed.Should().BeFalse();
        audit.Failures.Should().Contain(f => f.Contains("tray provides"));
    }

    [Fact]
    public void A_word_outside_the_dictionary_is_rejected()
    {
        var level = HealthyLevel();
        var broken = level with
        {
            Solution = level.Solution with
            {
                Words = new[]
                {
                    new CrosswordSolvedWord(0, Across, "合成一个整体。"),
                    new CrosswordSolvedWord(1, "合情合礼", "错字版。"),
                },
            },
        };

        var audit = CrosswordAudit.Check(broken, Dictionary());

        audit.Passed.Should().BeFalse();
        audit.Failures.Should().Contain(f => f.Contains("not in the dictionary"));
    }

    [Fact]
    public void A_word_that_disagrees_with_the_grid_is_rejected()
    {
        var level = HealthyLevel();
        // 词槽声称是「情理之中」,但格子里拼出来的是「合情合理」。
        var broken = level with
        {
            Solution = level.Solution with
            {
                Words = new[]
                {
                    new CrosswordSolvedWord(0, Across, "合成一个整体。"),
                    new CrosswordSolvedWord(1, "情理之中", "合乎情理。"),
                },
            },
        };

        var audit = CrosswordAudit.Check(broken, Dictionary());

        audit.Passed.Should().BeFalse();
        audit.Failures.Should().Contain(f => f.Contains("from the grid but claims"));
    }

    [Fact]
    public void A_given_cell_showing_the_wrong_character_is_rejected()
    {
        var level = HealthyLevel();
        var broken = level with
        {
            Layout = level.Layout with
            {
                Given = new[] { new CrosswordGivenCell(0, 0, "和") },
            },
        };

        var audit = CrosswordAudit.Check(broken, Dictionary());

        audit.Passed.Should().BeFalse();
        audit.Failures.Should().Contain(f => f.Contains("given cell"));
    }

    [Fact]
    public void A_word_without_an_explanation_is_rejected()
    {
        // 答对之后要能拿出那张"纸条";没有释义的词条不该进关卡。
        var level = HealthyLevel();
        var broken = level with
        {
            Solution = level.Solution with
            {
                Words = new[]
                {
                    new CrosswordSolvedWord(0, Across, "合成一个整体。"),
                    new CrosswordSolvedWord(1, Down, "   "),
                },
            },
        };

        var audit = CrosswordAudit.Check(broken, Dictionary());

        audit.Passed.Should().BeFalse();
        audit.Failures.Should().Contain(f => f.Contains("no explanation"));
    }

    [Fact]
    public void A_single_idiom_level_is_rejected()
    {
        var level = HealthyLevel();
        var broken = level with
        {
            Layout = level.Layout with
            {
                Slots = new[] { level.Layout.Slots[0] },
            },
        };

        var audit = CrosswordAudit.Check(broken, Dictionary());

        audit.Passed.Should().BeFalse();
        audit.Failures.Should().Contain(f => f.Contains("needs at least 2"));
    }

    [Fact]
    public void A_parallel_adjacent_grid_is_rejected()
    {
        // 两条横排词贴在相邻两行:并排的字连读是乱码,玩家分不清哪是真约束。
        var cells = new List<CrosswordCell>();
        var solutionCells = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < 4; i++)
        {
            cells.Add(new CrosswordCell(0, i));
            solutionCells[CrosswordSolution.Key(0, i)] = Across[i].ToString();
            cells.Add(new CrosswordCell(1, i));
            solutionCells[CrosswordSolution.Key(1, i)] = "情理之中"[i].ToString();
        }

        var layout = new CrosswordLayout(
            Rows: 2, Cols: 4, Cells: cells,
            Given: Array.Empty<CrosswordGivenCell>(),
            Tray: solutionCells.Values.ToList(),
            Slots: new[]
            {
                new CrosswordSlot(0, 0, 0, CrosswordDirection.Horizontal, 4),
                new CrosswordSlot(1, 1, 0, CrosswordDirection.Horizontal, 4),
            });

        var solution = new CrosswordSolution(solutionCells, new[]
        {
            new CrosswordSolvedWord(0, Across, "合成一个整体。"),
            new CrosswordSolvedWord(1, "情理之中", "合乎常情与道理。"),
        });

        var audit = CrosswordAudit.Check(new GeneratedLevel(layout, solution, 1), Dictionary());

        audit.Passed.Should().BeFalse();
        audit.Failures.Should().Contain(f => f.Contains("orthogonally adjacent"));
    }

    [Fact]
    public void A_cell_without_a_solution_character_is_rejected()
    {
        var level = HealthyLevel();
        var broken = level with
        {
            Layout = level.Layout with
            {
                Cells = level.Layout.Cells.Append(new CrosswordCell(3, 3)).ToList(),
            },
        };

        var audit = CrosswordAudit.Check(broken, Dictionary());

        audit.Passed.Should().BeFalse();
        audit.Failures.Should().Contain(f => f.Contains("no solution character"));
    }
}
