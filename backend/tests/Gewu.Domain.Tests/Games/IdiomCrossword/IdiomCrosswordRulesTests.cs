using System.Text.Json;
using Gewu.Domain.Games.IdiomCrossword;

namespace Gewu.Domain.Tests.Games.IdiomCrossword;

/// <summary>
/// 成语纵横规则的四个操作。用一个手搭的小网格:
/// <c>合而为一</c> 横排在第 0 行,<c>合情合理</c> 竖排在第 0 列,共用 (0,0) 的「合」。
/// </summary>
public class IdiomCrosswordRulesTests
{
    private static readonly IdiomCrosswordRules Rules = new();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const string Across = "合而为一";
    private const string Down = "合情合理";

    private static string SolutionJson()
    {
        var cells = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < Across.Length; i++)
        {
            cells[CrosswordSolution.Key(0, i)] = Across[i].ToString();
        }
        for (var i = 0; i < Down.Length; i++)
        {
            cells[CrosswordSolution.Key(i, 0)] = Down[i].ToString();
        }

        var solution = new CrosswordSolution(cells, new[]
        {
            new CrosswordSolvedWord(0, Across, "合成一个整体。"),
            new CrosswordSolvedWord(1, Down, "合乎情理。"),
        });

        return JsonSerializer.Serialize(solution, Json);
    }

    private static string LayoutJson(params (int Row, int Col, string Char)[] given)
    {
        var cells = new List<CrosswordCell>();
        for (var i = 0; i < Across.Length; i++)
        {
            cells.Add(new CrosswordCell(0, i));
        }
        for (var i = 1; i < Down.Length; i++)
        {
            cells.Add(new CrosswordCell(i, 0));
        }

        var layout = new CrosswordLayout(
            Rows: 4,
            Cols: 4,
            Cells: cells.OrderBy(c => c.Row).ThenBy(c => c.Col).ToList(),
            Given: given.Select(g => new CrosswordGivenCell(g.Row, g.Col, g.Char)).ToList(),
            Tray: new[] { "而", "为", "一", "情", "合", "理" },
            Slots: new[]
            {
                new CrosswordSlot(0, 0, 0, CrosswordDirection.Horizontal, 4),
                new CrosswordSlot(1, 0, 0, CrosswordDirection.Vertical, 4),
            });

        return JsonSerializer.Serialize(layout, Json);
    }

    private static string FullSubmission(bool correct)
    {
        var cells = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < Across.Length; i++)
        {
            cells[CrosswordSolution.Key(0, i)] = Across[i].ToString();
        }
        for (var i = 1; i < Down.Length; i++)
        {
            cells[CrosswordSolution.Key(i, 0)] = Down[i].ToString();
        }
        if (!correct)
        {
            cells[CrosswordSolution.Key(0, 3)] = "二"; // 把「一」改错一格
        }

        return JsonSerializer.Serialize(new CrosswordSubmission(cells), Json);
    }

    private static string Partial(int slotIndex, string word)
        => JsonSerializer.Serialize(new CrosswordPartialSubmission(slotIndex, word), Json);

    // ---- Validate ----

    [Fact]
    public void Validate_passes_an_exact_grid()
        => Rules.Validate(SolutionJson(), FullSubmission(correct: true))
            .IsCorrect.Should().BeTrue();

    [Fact]
    public void Validate_fails_on_a_single_wrong_cell()
        => Rules.Validate(SolutionJson(), FullSubmission(correct: false))
            .IsCorrect.Should().BeFalse();

    [Fact]
    public void Validate_fails_on_an_incomplete_grid()
    {
        var partial = JsonSerializer.Serialize(new CrosswordSubmission(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CrosswordSolution.Key(0, 0)] = "合",
            }), Json);

        Rules.Validate(SolutionJson(), partial).IsCorrect.Should().BeFalse();
    }

    [Fact]
    public void Validate_treats_malformed_json_as_incorrect_rather_than_throwing()
    {
        // 载荷来自玩家,畸形输入是正常情况:一律当作"不正确",不让坏 JSON 变成 500。
        var act = () => Rules.Validate(SolutionJson(), "{not json");
        act.Should().NotThrow();
        Rules.Validate(SolutionJson(), "{not json").IsCorrect.Should().BeFalse();
    }

    // ---- CheckPartial ----

    [Fact]
    public void CheckPartial_returns_the_word_and_its_explanation_when_correct()
    {
        var result = Rules.CheckPartial(SolutionJson(), Partial(0, Across));

        result.IsCorrect.Should().BeTrue();
        result.PayloadJson.Should().NotBeNull();

        var payload = JsonSerializer.Deserialize<CrosswordSolvedWord>(result.PayloadJson!, Json)!;
        payload.Word.Should().Be(Across);
        payload.Explanation.Should().Be("合成一个整体。");
    }

    [Fact]
    public void CheckPartial_returns_no_payload_when_wrong()
    {
        var result = Rules.CheckPartial(SolutionJson(), Partial(0, "合而为二"));

        result.IsCorrect.Should().BeFalse();
        // 答错附带任何内容都等于借错误路径泄题。
        result.PayloadJson.Should().BeNull();
    }

    [Fact]
    public void CheckPartial_rejects_an_unknown_slot_index()
    {
        var result = Rules.CheckPartial(SolutionJson(), Partial(99, Across));

        result.IsCorrect.Should().BeFalse();
        result.PayloadJson.Should().BeNull();
    }

    [Fact]
    public void CheckPartial_checks_the_named_slot_not_just_any_slot()
    {
        // 把横排的答案报到竖排的槽上 —— 必须判错,否则玩家可以拿一条成语骗过所有槽。
        var result = Rules.CheckPartial(SolutionJson(), Partial(1, Across));

        result.IsCorrect.Should().BeFalse();
    }

    // ---- Hint ----

    [Fact]
    public void Hint_walks_forward_as_the_player_fills_cells()
    {
        // 预填 (0,0)。每次请求都带上此刻已填的格,所以提示依次落在下一个真正空着的格 ——
        // 揭示位置由**玩家的进度**决定,不再由请求次数决定。
        var layout = LayoutJson((0, 0, "合"));
        var solution = SolutionJson();

        var first = Reveal(Rules.Hint(solution, layout, null));
        var second = Reveal(Rules.Hint(solution, layout, "{\"filled\":[\"0,1\"]}"));
        var third = Reveal(Rules.Hint(solution, layout, "{\"filled\":[\"0,1\",\"0,2\"]}"));

        (first.Row, first.Col, first.Char).Should().Be((0, 1, "而"));
        (second.Row, second.Col, second.Char).Should().Be((0, 2, "为"));
        (third.Row, third.Col, third.Char).Should().Be((0, 3, "一"));
    }

    [Fact]
    public void Hint_still_returns_a_cell_when_no_state_is_reported()
    {
        // 一个没上报盘面状态的客户端(比如旧版本)仍然应该拿到提示,而不是 400。
        var act = () => Rules.Hint(SolutionJson(), LayoutJson(), null);
        act.Should().NotThrow();
        Reveal(Rules.Hint(SolutionJson(), LayoutJson(), null)).Char.Should().NotBeEmpty();
    }

    private static CrosswordRevealedCell Reveal(Gewu.Domain.Puzzles.PuzzleHintResult hint)
        => JsonSerializer.Deserialize<CrosswordRevealedCell>(hint.RevealedJson, Json)!;

    // ---- Score ----

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(1, 0, 2)]
    [InlineData(0, 1, 2)]
    [InlineData(1, 1, 2)]
    [InlineData(0, 3, 1)]
    [InlineData(2, 2, 1)]
    public void Score_matches_the_prototype(int hints, int mistakes, int expected)
        => Rules.Score(hints, mistakes, TimeSpan.FromMinutes(3)).Should().Be(expected);

    [Fact]
    public void Score_ignores_elapsed_time()
    {
        // 与原型一致:想得慢不该掉星。用时被记录下来做最好成绩的次级排序,不参与计分。
        Rules.Score(0, 0, TimeSpan.FromSeconds(5))
            .Should().Be(Rules.Score(0, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void GameKey_matches_the_registry_and_the_web_manifest()
        => Rules.GameKey.Should().Be("idiom-crossword");
}
