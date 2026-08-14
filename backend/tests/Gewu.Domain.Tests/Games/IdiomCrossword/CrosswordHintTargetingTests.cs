using System.Text.Json;
using Gewu.Domain.Games.IdiomCrossword;

namespace Gewu.Domain.Tests.Games.IdiomCrossword;

/// <summary>
/// 提示定位。
/// <para>
/// 这组测试存在的理由是一个实测出来的失效:提示原本按阅读顺序推进,而玩家也是自上而下填,
/// 两者同向,所以等玩家卡住时,提示能够到的格子全是他已经解开的。第 5 关实测中,第一个
/// 有用的提示要点到第 14 次 —— 前 13 次照扣星、照计数,屏幕上什么都不变。
/// </para>
/// </summary>
public class CrosswordHintTargetingTests
{
    private static readonly IdiomCrosswordRules Rules = new();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// 复刻第 5 关的形状:七格竖行 + 底部一行,预填两格。
    /// 竖行在阅读序上排在前面,底行排在最后 —— 正是出问题的那个几何。
    /// </summary>
    private const string Across = "木人石心";

    private static (string Layout, string Solution) Level()
    {
        var cells = new List<CrosswordCell>();
        var solutionCells = new Dictionary<string, string>(StringComparer.Ordinal);

        // 竖行:(0,3) (1,3) (2,3) —— 玩家先解开的部分
        var down = "用尽心";
        for (var i = 0; i < down.Length; i++)
        {
            cells.Add(new CrosswordCell(i, 3));
            solutionCells[CrosswordSolution.Key(i, 3)] = down[i].ToString();
        }

        // 底行:(3,0) (3,1) (3,2) (3,3) —— 玩家卡住的部分,(3,3) 是交叉格
        for (var i = 0; i < Across.Length; i++)
        {
            var cell = new CrosswordCell(3, i);
            if (i != 3)
            {
                cells.Add(cell);
            }
            solutionCells[CrosswordSolution.Key(3, i)] = Across[i].ToString();
        }
        cells.Add(new CrosswordCell(3, 3));

        var layout = new CrosswordLayout(
            Rows: 4,
            Cols: 4,
            Cells: cells.OrderBy(c => c.Row).ThenBy(c => c.Col).ToList(),
            Given: new[] { new CrosswordGivenCell(0, 3, "用") },
            Tray: new[] { "尽", "心", "木", "人", "石" },
            Slots: new[]
            {
                new CrosswordSlot(0, 0, 3, CrosswordDirection.Vertical, 4),
                new CrosswordSlot(1, 3, 0, CrosswordDirection.Horizontal, 4),
            });

        var solution = new CrosswordSolution(solutionCells, new[]
        {
            new CrosswordSolvedWord(0, "用尽心机", "费尽心思。"),
            new CrosswordSolvedWord(1, Across, "形容人冷酷无情。"),
        });

        return (JsonSerializer.Serialize(layout, Json), JsonSerializer.Serialize(solution, Json));
    }

    private static string State(IEnumerable<string>? filled, string? selected = null)
        => JsonSerializer.Serialize(
            new CrosswordHintState(filled?.ToList(), selected), Json);

    private static CrosswordRevealedCell Reveal(string? stateJson)
    {
        var (layout, solution) = Level();
        var hint = Rules.Hint(solution, layout, stateJson);
        return JsonSerializer.Deserialize<CrosswordRevealedCell>(hint.RevealedJson, Json)!;
    }

    // ---- 回归:报出来的那个 bug ----

    [Fact]
    public void The_reported_bug_the_hint_no_longer_lands_on_an_already_solved_cell()
    {
        // 玩家已解开竖行(1,3)(2,3),只剩底行左边三格。
        var filled = new[] { "0,3", "1,3", "2,3", "3,3" };

        var revealed = Reveal(State(filled));

        // 修复前:会揭 (1,3)「尽」—— 玩家早填好并锁定了的格子,屏幕上毫无变化。
        filled.Should().NotContain(CrosswordSolution.Key(revealed.Row, revealed.Col));
        revealed.Row.Should().Be(3);
        revealed.Col.Should().BeLessThan(3);
    }

    // ---- 揭示优先级 ----

    [Fact]
    public void A_selected_cell_wins_over_reading_order()
    {
        var revealed = Reveal(State(filled: null, selected: "3,2"));

        (revealed.Row, revealed.Col, revealed.Char).Should().Be((3, 2, "石"));
    }

    [Fact]
    public void A_selected_cell_is_revealed_even_when_it_already_holds_a_character()
    {
        // 盯着一个填错的格子要提示,想解的正是那一格。
        var revealed = Reveal(State(new[] { "0,3", "3,1" }, selected: "3,1"));

        (revealed.Row, revealed.Col, revealed.Char).Should().Be((3, 1, "人"));
    }

    [Fact]
    public void Without_a_selection_the_first_unfilled_cell_is_revealed()
    {
        var revealed = Reveal(State(new[] { "0,3", "1,3" }));

        // 阅读序:(0,3)given (1,3)filled (2,3) ← 第一个未填
        (revealed.Row, revealed.Col, revealed.Char).Should().Be((2, 3, "心"));
    }

    [Fact]
    public void An_empty_board_reveals_the_first_revealable_cell()
    {
        var revealed = Reveal(State(filled: null));

        // (0,3) 是预填格,所以第一个可揭示格是 (1,3)。
        (revealed.Row, revealed.Col).Should().Be((1, 3));
    }

    // ---- 降级 ----

    [Fact]
    public void A_selection_pointing_at_a_pre_filled_cell_is_ignored()
    {
        var revealed = Reveal(State(new[] { "0,3" }, selected: "0,3"));

        // 预填格不在可揭示集合里 —— 退到第一个未填格,而不是报错。
        (revealed.Row, revealed.Col).Should().Be((1, 3));
    }

    [Fact]
    public void A_selection_pointing_at_a_nonexistent_cell_is_ignored()
    {
        var revealed = Reveal(State(new[] { "0,3" }, selected: "9,9"));

        (revealed.Row, revealed.Col).Should().Be((1, 3));
    }

    [Fact]
    public void A_selection_at_the_origin_is_honoured()
    {
        // CrosswordCell 是 record struct,`default` 就是 (0,0) —— 用 `!= default`
        // 当"找到了没"的哨兵会把这一格误判成"没找到"。
        var revealed = Reveal(State(filled: null, selected: "3,0"));

        (revealed.Row, revealed.Col, revealed.Char).Should().Be((3, 0, "木"));
    }

    [Fact]
    public void A_full_board_falls_back_to_the_first_revealable_cell()
    {
        // 满盘通常意味着填错了 —— 这时用正确字覆盖一格正是最有用的一步。
        var all = new[] { "0,3", "1,3", "2,3", "3,0", "3,1", "3,2", "3,3" };

        var revealed = Reveal(State(all));

        (revealed.Row, revealed.Col).Should().Be((1, 3));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("[1,2,3]")]
    public void A_missing_or_malformed_state_still_yields_a_hint(string? stateJson)
    {
        // 一个没更新的客户端应该拿到提示,而不是 400。
        var act = () => Reveal(stateJson);

        act.Should().NotThrow();
        var revealed = Reveal(stateJson);
        revealed.Char.Should().NotBeEmpty();
    }
}
