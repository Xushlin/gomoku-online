using System.Text.Json;
using Gewu.Domain.Games.IdiomGuess;
using Gewu.Domain.Puzzles;

namespace Gewu.Domain.Tests.Games.IdiomGuess;

/// <summary>
/// 猜成语的四个操作。
/// <para>
/// 两条最要紧的:**答错时不带任何载荷**(否则错误路径就是泄题通道),以及
/// **出处缺失照样答得对**(可用池里 252 条没有出处,产物里也有一条)。
/// </para>
/// </summary>
public class IdiomGuessRulesTests
{
    private static readonly IdiomGuessRules Rules = new();

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, IdiomGuessRules.Json);

    /// <summary>两道题:一条有出处,一条**没有** —— 后者是本组好几条断言的前提。</summary>
    private static (string Layout, string Solution) Level()
    {
        var layout = new IdiomGuessLayout(new[]
        {
            new IdiomGuessPuzzle(0, "形容一下子出了名。", new string?[] { "一", "鸣", null, "人" }),
            new IdiomGuessPuzzle(1, "比喻做事有始有终。", new string?[] { null, "始", "有", "终" }),
        });
        var solution = new IdiomGuessSolution(new[]
        {
            new IdiomGuessAnswer(0, "一鸣惊人", "《史记·滑稽列传》"),
            new IdiomGuessAnswer(1, "有始有终", null),
        });
        return (Json(layout), Json(solution));
    }

    // ---- CheckPartial ----

    [Fact]
    public void A_correct_answer_comes_back_with_its_derivation()
    {
        var (layout, solution) = Level();

        var result = Rules.CheckPartial(
            solution, layout, Json(new IdiomGuessPartialSubmission(0, "一鸣惊人")));

        result.IsCorrect.Should().BeTrue();
        result.PayloadJson.Should().NotBeNull();
        result.PayloadJson.Should().Contain("史记");
    }

    /// <summary>
    /// **出处缺失照样答得对。** 可用池 9,615 条里有 252 条没有出处,而产物里就有一条 ——
    /// 一个假定出处一定在的实现会在那一条上炸,而它看起来只是"这道题打不开"。
    /// </summary>
    [Fact]
    public void An_idiom_with_no_derivation_is_still_answerable()
    {
        var (layout, solution) = Level();

        var result = Rules.CheckPartial(
            solution, layout, Json(new IdiomGuessPartialSubmission(1, "有始有终")));

        result.IsCorrect.Should().BeTrue();
        var solved = JsonSerializer.Deserialize<IdiomGuessSolved>(
            result.PayloadJson!, IdiomGuessRules.Json);
        solved!.Word.Should().Be("有始有终");
        solved.Derivation.Should().BeNull("没有出处就是 null —— 空串会让前端画一张空纸条");
    }

    [Fact]
    public void A_wrong_answer_carries_no_payload_at_all()
    {
        var (layout, solution) = Level();

        var result = Rules.CheckPartial(
            solution, layout, Json(new IdiomGuessPartialSubmission(0, "一鸣惊天")));

        result.IsCorrect.Should().BeFalse();
        result.PayloadJson.Should().BeNull(
            "答错时附带任何内容都等于借错误路径泄题 —— 这是接口写下的规矩");
    }

    [Fact]
    public void A_malformed_payload_is_wrong_not_an_exception()
    {
        var (layout, solution) = Level();

        var act = () => Rules.CheckPartial(solution, layout, "{ not json");

        act.Should().NotThrow();
        Rules.CheckPartial(solution, layout, "{ not json").IsCorrect.Should().BeFalse();
    }

    // ---- Validate ----

    [Fact]
    public void All_of_them_right_is_a_pass()
    {
        var (layout, solution) = Level();
        var submission = Json(new IdiomGuessSubmission(new Dictionary<string, string>
        {
            ["0"] = "一鸣惊人",
            ["1"] = "有始有终",
        }));

        Rules.Validate(solution, layout, submission).IsCorrect.Should().BeTrue();
    }

    [Fact]
    public void One_of_them_wrong_is_not_a_pass()
    {
        var (layout, solution) = Level();
        var submission = Json(new IdiomGuessSubmission(new Dictionary<string, string>
        {
            ["0"] = "一鸣惊人",
            ["1"] = "有始无终",
        }));

        Rules.Validate(solution, layout, submission).IsCorrect.Should().BeFalse();
    }

    /// <summary>少答一条也不算通关 —— 否则交一份空提交就能过。</summary>
    [Fact]
    public void An_incomplete_submission_is_not_a_pass()
    {
        var (layout, solution) = Level();
        var submission = Json(new IdiomGuessSubmission(new Dictionary<string, string>
        {
            ["0"] = "一鸣惊人",
        }));

        Rules.Validate(solution, layout, submission).IsCorrect.Should().BeFalse();
    }

    // ---- Hint ----

    [Fact]
    public void A_hint_reveals_exactly_one_character_and_it_is_the_right_one()
    {
        var (layout, solution) = Level();

        var hint = Rules.Hint(solution, layout, null);

        var revealed = JsonSerializer.Deserialize<IdiomGuessRevealed>(
            hint.RevealedJson, IdiomGuessRules.Json);
        revealed!.PuzzleIndex.Should().Be(0);
        revealed.Position.Should().Be(2);
        revealed.Char.Should().Be("惊");
    }

    /// <summary>
    /// 玩家指着哪一格就揭哪一格 —— 而这一条同时钉住那个**下标哨兵**。
    /// <para>
    /// 第 0 题第 0 位是一个**合法**的空格,而值元组的 `default` 恰好是 `(0, 0, null)`。
    /// 拿 `== default` 当"没找到"的哨兵,会在玩家正好指着第一题第一格时把它当成没找到。
    /// 本实现第一版就是那么写的。
    /// </para>
    /// </summary>
    [Fact]
    public void The_hint_follows_the_players_cursor_including_the_very_first_blank()
    {
        var (layout, solution) = Level();

        var hint = Rules.Hint(
            solution, layout, Json(new IdiomGuessHintState(Selected: "1:0", Filled: null)));

        var revealed = JsonSerializer.Deserialize<IdiomGuessRevealed>(
            hint.RevealedJson, IdiomGuessRules.Json);
        revealed!.PuzzleIndex.Should().Be(1);
        revealed.Position.Should().Be(0);
        revealed.Char.Should().Be("有");
    }

    [Fact]
    public void A_stale_cursor_degrades_to_a_sensible_hint_rather_than_an_error()
    {
        var (layout, solution) = Level();

        var act = () => Rules.Hint(
            solution, layout, Json(new IdiomGuessHintState(Selected: "9:9", Filled: null)));

        act.Should().NotThrow("一个没更新的客户端应该拿到提示,而不是 400");
        var revealed = JsonSerializer.Deserialize<IdiomGuessRevealed>(
            act().RevealedJson, IdiomGuessRules.Json);
        revealed!.Char.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void A_hint_skips_the_blanks_the_player_has_already_filled()
    {
        var (layout, solution) = Level();

        var hint = Rules.Hint(
            solution, layout,
            Json(new IdiomGuessHintState(Selected: null, Filled: new[] { "0:2" })));

        var revealed = JsonSerializer.Deserialize<IdiomGuessRevealed>(
            hint.RevealedJson, IdiomGuessRules.Json);
        revealed!.PuzzleIndex.Should().Be(1, "第 0 题那个空玩家已经填了");
    }

    // ---- Score ----

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(1, 0, 2)]
    [InlineData(0, 2, 2)]
    [InlineData(2, 1, 1)]
    public void Stars_come_from_hints_plus_mistakes(int hints, int mistakes, int expected)
    {
        var input = new PuzzleScoreInput(
            hints, mistakes, TimeSpan.FromMinutes(5), "{}", "{}", "{}");

        Rules.Score(input).Should().Be(expected);
    }

    /// <summary>用时不参与计分 —— 想清楚每一步的玩家不该因为想得慢而掉星。</summary>
    [Fact]
    public void Time_does_not_cost_stars()
    {
        var quick = new PuzzleScoreInput(0, 0, TimeSpan.FromSeconds(5), "{}", "{}", "{}");
        var slow = new PuzzleScoreInput(0, 0, TimeSpan.FromHours(2), "{}", "{}", "{}");

        Rules.Score(quick).Should().Be(Rules.Score(slow));
    }

    [Fact]
    public void The_game_key_is_the_one_the_registry_resolves()
    {
        Rules.GameKey.Should().Be("idiom-guess");
    }
}
