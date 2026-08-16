using System.Diagnostics;
using System.Text.Json;
using Gewu.Domain.Games.Klotski;
using Gewu.Domain.Puzzles;

namespace Gewu.Domain.Tests.Games.Klotski;

/// <summary>
/// 华容道的规则。
/// <para>
/// **本文件不引用任何出版物上的步数。** 经典局面的公开数字随数法而异(连滑算一步 vs
/// 一格一步),抄进来既不可复现又可能不自洽 —— 与 <c>add-xiangqi-ai</c> 拒绝声称
/// 「不可战胜」同一条。凡是出现步数的地方,那个数都由求解器当场算出。
/// </para>
/// </summary>
public class KlotskiRulesTests
{
    private static readonly KlotskiRules Rules = new();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    // ---- 布局夹具 ----

    /// <summary>横刀立马:曹操居上正中,四将竖立,关羽横卧,四卒填底,底部两格空。</summary>
    private static string ClassicLayout() => Layout(
        [
            new("cao", 0, 1, 2, 2, true),
            new("zhang", 0, 0, 2, 1),
            new("ma", 0, 3, 2, 1),
            new("zhao", 2, 0, 2, 1),
            new("huang", 2, 3, 2, 1),
            new("guan", 2, 1, 1, 2),
            new("s1", 3, 1, 1, 1),
            new("s2", 3, 2, 1, 1),
            new("s3", 4, 0, 1, 1),
            new("s4", 4, 3, 1, 1),
        ]);

    /// <summary>差一步到位:曹操在 (2,1),下面整排空着。</summary>
    private static string OneMoveLayout() => Layout(
        [
            new("cao", 2, 1, 2, 2, true),
            new("s1", 0, 0, 1, 1),
        ]);

    private static string Layout(IReadOnlyList<KlotskiLayoutPiece> pieces, int rows = 5, int cols = 4)
        => JsonSerializer.Serialize(
            new KlotskiLayout(rows, cols, new KlotskiExit(3, 1), pieces), Json);

    private static string Solution(int minMoves)
        => JsonSerializer.Serialize(new KlotskiSolution(minMoves), Json);

    private static string Submission(IEnumerable<KlotskiMove> moves)
        => JsonSerializer.Serialize(new KlotskiSubmission([.. moves]), Json);

    private static string State(params (string Id, int Row, int Col)[] pieces)
        => JsonSerializer.Serialize(
            new KlotskiState([.. pieces.Select(p => new KlotskiStatePiece(p.Id, p.Row, p.Col))]),
            Json);

    private static IReadOnlyList<KlotskiMove> Optimal(string layout)
    {
        var solution = KlotskiLevels.Solve(layout);
        solution.Should().NotBeNull("每个夹具关卡都必须有解");
        return solution!;
    }

    // ---- Validate ----

    [Fact]
    public void An_optimal_solution_is_accepted()
    {
        var layout = ClassicLayout();
        var moves = Optimal(layout);

        Rules.Validate(Solution(moves.Count), layout, Submission(moves))
            .IsCorrect.Should().BeTrue();
    }

    [Fact]
    public void One_illegal_step_voids_the_whole_submission()
    {
        // 服务端不接受它重放不出来的东西 —— 哪怕跳过那一步末态确实到位。
        var layout = ClassicLayout();
        var moves = Optimal(layout).ToList();
        moves.Insert(0, new KlotskiMove("cao", 0, 1));   // 曹操右边是马超,推不动

        Rules.Validate(Solution(1), layout, Submission(moves)).IsCorrect.Should().BeFalse();
    }

    [Fact]
    public void Legal_moves_that_do_not_reach_the_exit_are_not_a_solve()
    {
        var layout = ClassicLayout();
        var prefix = Optimal(layout).Take(3);

        Rules.Validate(Solution(1), layout, Submission(prefix)).IsCorrect.Should().BeFalse();
    }

    [Fact]
    public void An_empty_submission_is_not_a_solve()
        => Rules.Validate(Solution(1), ClassicLayout(), Submission([]))
            .IsCorrect.Should().BeFalse();

    [Fact]
    public void An_already_solved_layout_accepts_zero_moves()
    {
        // 对照组:上一条之所以不通关,是因为曹操没到出口,不是因为「零步永远不算」。
        var layout = Layout([new("cao", 3, 1, 2, 2, true)]);

        Rules.Validate(Solution(1), layout, Submission([])).IsCorrect.Should().BeTrue();
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Malformed_submissions_are_wrong_rather_than_thrown(string submission)
    {
        var act = () => Rules.Validate(Solution(1), ClassicLayout(), submission);

        act.Should().NotThrow();
        Rules.Validate(Solution(1), ClassicLayout(), submission).IsCorrect.Should().BeFalse();
    }

    [Fact]
    public void A_broken_layout_is_a_miss_rather_than_a_crash()
    {
        // 两枚子重叠 —— 一份坏关卡是数据问题,不该表现为 500。
        var overlapping = Layout(
            [new("cao", 0, 0, 2, 2, true), new("s1", 1, 1, 1, 1)]);

        var act = () => Rules.Validate(Solution(1), overlapping, Submission([]));

        act.Should().NotThrow();
        Rules.Validate(Solution(1), overlapping, Submission([])).IsCorrect.Should().BeFalse();
    }

    [Theory]
    [InlineData(2, 0)]   // 一步跨两格
    [InlineData(1, 1)]   // 斜着走
    [InlineData(0, 0)]   // 原地不动
    public void Only_single_orthogonal_steps_are_legal(int dr, int dc)
    {
        var layout = OneMoveLayout();

        Rules.Validate(Solution(1), layout, Submission([new KlotskiMove("cao", dr, dc)]))
            .IsCorrect.Should().BeFalse();
    }

    [Fact]
    public void A_move_naming_a_piece_that_does_not_exist_is_illegal()
        => Rules.Validate(
                Solution(1), OneMoveLayout(), Submission([new KlotskiMove("nobody", 1, 0)]))
            .IsCorrect.Should().BeFalse();

    // ---- 求解器 ----

    [Fact]
    public void The_solver_finds_the_shortest_route_out()
    {
        // 曹操在 (2,1),出口在 (3,1) —— 正下方全空,一步。
        KlotskiLevels.MinMoves(OneMoveLayout()).Should().Be(1);
    }

    [Fact]
    public void Every_solution_the_solver_returns_actually_solves()
    {
        var layout = ClassicLayout();
        var moves = Optimal(layout);

        // 独立于 KlotskiRules 再重放一次:两条路径都必须同意。
        KlotskiLevels.Replay(layout, moves).Should().BeTrue();
        moves.Should().OnlyContain(m => m.IsSingleStep);
    }

    [Fact]
    public void Removing_a_blocker_never_makes_a_layout_harder()
    {
        // 关卡产物的难度梯度就是这样派生的:少一枚子严格意味着更多空格、更少约束。
        // 这条把那个推理变成一个可执行的断言,而不是设计时的一句话。
        var full = KlotskiLevels.MinMoves(ClassicLayout());
        var fewer = KlotskiLevels.MinMoves(Layout(
            [
                new("cao", 0, 1, 2, 2, true),
                new("zhang", 0, 0, 2, 1),
                new("ma", 0, 3, 2, 1),
                new("zhao", 2, 0, 2, 1),
                new("huang", 2, 3, 2, 1),
                new("guan", 2, 1, 1, 2),
                new("s3", 4, 0, 1, 1),
                new("s4", 4, 3, 1, 1),
            ]));

        full.Should().NotBeNull();
        fewer.Should().NotBeNull();
        fewer.Should().BeLessThanOrEqualTo(full!.Value);
    }

    [Fact]
    public void A_layout_with_no_empty_square_has_no_solution()
    {
        // 盘面塞满:曹操占 4 格,其余 16 格各一枚卒,没有空格 → 谁都动不了。
        // 「封死」这件事必须是**结构上**确定的,不能靠我摆几枚子的直觉 ——
        // 一条只有在我猜对时才成立的断言不是断言。
        var pieces = new List<KlotskiLayoutPiece> { new("cao", 0, 0, 2, 2, true) };
        for (var row = 0; row < 5; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                if (row < 2 && col < 2)
                {
                    continue;   // 曹操占着
                }
                pieces.Add(new KlotskiLayoutPiece($"p{row}{col}", row, col, 1, 1));
            }
        }
        var full = Layout(pieces);

        KlotskiLevels.Solve(full).Should().BeNull();
        Rules.Validate(Solution(1), full, Submission([])).IsCorrect.Should().BeFalse();
    }

    // ---- Hint ----

    [Fact]
    public void A_hint_is_the_next_step_of_a_shortest_route_from_where_the_player_is()
    {
        var layout = ClassicLayout();
        var moves = Optimal(layout);

        // 先走 10 步,把玩家挪到一个不在「初始最优路径起点」的位置。
        var walked = moves.Take(10).ToList();
        var state = StateAfter(layout, walked);
        var remainingBefore = DistanceFrom(layout, walked);

        var hint = JsonSerializer.Deserialize<KlotskiMove>(
            Rules.Hint(Solution(moves.Count), layout, state).RevealedJson, Json);

        hint.IsSingleStep.Should().BeTrue();

        // 判据不是「看起来像一步」,而是**走完之后到出口的最短距离恰好减一** ——
        // 这正是「最短路径上的下一步」的定义。
        var remainingAfter = DistanceFrom(layout, [.. walked, hint]);
        remainingAfter.Should().Be(remainingBefore - 1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("{\"pieces\":[{\"id\":\"cao\",\"row\":99,\"col\":99}]}")]
    public void A_hint_degrades_instead_of_failing(string? state)
    {
        var layout = ClassicLayout();

        var act = () => Rules.Hint(Solution(1), layout, state);

        act.Should().NotThrow();
        var revealed = JsonSerializer.Deserialize<KlotskiMove>(
            Rules.Hint(Solution(1), layout, state).RevealedJson, Json);
        revealed.IsSingleStep.Should().BeTrue("退化到初始布局的第一步,而不是返回空");
    }

    [Fact]
    public void The_reported_state_cannot_redefine_the_pieces()
    {
        // 上报的只是「哪枚子现在在哪」。尺寸与目标标记始终以关卡为准 —— 否则客户端
        // 可以上报一个 1×1 的「曹操」然后要一条一步的提示。
        var layout = ClassicLayout();
        var lying = "{\"pieces\":[{\"id\":\"cao\",\"row\":3,\"col\":1,\"height\":1,\"width\":1}]}";

        var hint = Rules.Hint(Solution(1), layout, lying);

        // 曹操 2×2 放到 (3,1) 会和底部的卒重叠 → 上报局面不合法 → 退回初始布局。
        hint.RevealedJson.Should().NotBe("{}");
    }

    // ---- CheckPartial ----

    [Fact]
    public void A_legal_prefix_checks_out()
    {
        var layout = ClassicLayout();
        var prefix = Optimal(layout).Take(5);

        var result = Rules.CheckPartial(Solution(1), layout, Submission(prefix));

        result.IsCorrect.Should().BeTrue();
        result.PayloadJson.Should().Contain("caoCaoOut");
    }

    [Fact]
    public void An_illegal_prefix_does_not()
        => Rules.CheckPartial(
                Solution(1), ClassicLayout(), Submission([new KlotskiMove("cao", 0, 1)]))
            .IsCorrect.Should().BeFalse();

    [Fact]
    public void The_payload_says_when_the_target_is_out()
    {
        var layout = OneMoveLayout();
        var result = Rules.CheckPartial(
            Solution(1), layout, Submission([new KlotskiMove("cao", 1, 0)]));

        result.PayloadJson.Should().Contain("true");
    }

    // ---- Score ----

    private static PuzzleScoreInput ScoreInput(int minMoves, int moves, int hints, int mistakes = 0)
        => new(
            hints,
            mistakes,
            TimeSpan.FromMinutes(5),
            ClassicLayout(),
            Solution(minMoves),
            Submission(Enumerable.Repeat(new KlotskiMove("s3", 0, 1), moves)));

    [Theory]
    [InlineData(100, 100, 0, 3)]   // 恰好最优,没用提示
    [InlineData(100, 100, 1, 2)]   // 最优但用了提示 —— 拿不到三星
    [InlineData(100, 140, 0, 2)]   // 1.4 倍边界内
    [InlineData(100, 141, 0, 1)]   // 越过 1.4 倍
    [InlineData(100, 120, 3, 1)]   // 提示超预算
    [InlineData(100, 200, 0, 1)]   // 两倍步数
    public void Score_follows_the_step_count(int minMoves, int moves, int hints, int expected)
        => Rules.Score(ScoreInput(minMoves, moves, hints)).Should().Be(expected);

    [Fact]
    public void Score_ignores_elapsed_time()
    {
        var fast = ScoreInput(100, 100, 0) with { Duration = TimeSpan.FromSeconds(30) };
        var slow = ScoreInput(100, 100, 0) with { Duration = TimeSpan.FromHours(3) };

        Rules.Score(fast).Should().Be(Rules.Score(slow));
    }

    [Fact]
    public void Score_ignores_mistakes()
    {
        // 华容道的 Mistakes 结构性地恒为 0 —— 那个计数器只有客户端调 check 才增长,
        // 而它没有理由调。把一个永远为 0 的量写进公式等于写一段永不执行的代码。
        Rules.Score(ScoreInput(100, 100, 0, mistakes: 0))
            .Should().Be(Rules.Score(ScoreInput(100, 100, 0, mistakes: 5)));
    }

    [Fact]
    public void A_broken_level_scores_one_star_rather_than_throwing()
    {
        // 计分是通关之后的事。让玩家的一次通关因为一份坏数据变成 500 是最糟的结果。
        var input = ScoreInput(100, 100, 0) with { SolutionJson = "{not json" };

        Rules.Score(input).Should().Be(1);
    }

    [Fact]
    public void GameKey_matches_the_registry()
        => Rules.GameKey.Should().Be("klotski");

    // ---- 性能:记录下来,不藏 ----

    [Fact]
    public void A_hint_on_the_classic_layout_stays_interactive()
    {
        // 提示要跑一次完整搜索。这条不是为了卡一个漂亮的数,而是为了让「它有多慢」
        // 是一件被记录下来的事:超了就会红,红了就得写进 tasks 而不是悄悄放宽。
        var layout = ClassicLayout();
        var sw = Stopwatch.StartNew();
        Rules.Hint(Solution(116), layout, null);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(3000,
            "一次提示是玩家主动发起、还要扣一颗星的操作,但它仍然得在一次请求里返回");
    }

    // ---- 辅助 ----

    private static string StateAfter(string layout, IReadOnlyList<KlotskiMove> moves)
    {
        var positions = PositionsAfter(layout, moves);
        return State([.. positions.Select(p => (p.Key, p.Value.Row, p.Value.Col))]);
    }

    private static int DistanceFrom(string layout, IReadOnlyList<KlotskiMove> moves)
    {
        var positions = PositionsAfter(layout, moves);
        var original = JsonSerializer.Deserialize<KlotskiLayout>(layout, Json)!;
        var moved = original.Pieces!
            .Select(p => p with { Row = positions[p.Id].Row, Col = positions[p.Id].Col })
            .ToList();

        var distance = KlotskiLevels.MinMoves(JsonSerializer.Serialize(
            new KlotskiLayout(original.Rows, original.Cols, original.Exit, moved), Json));

        distance.Should().NotBeNull();
        return distance!.Value;
    }

    /// <summary>在测试里独立地重放一遍 —— 不借规则的实现来验规则。</summary>
    private static Dictionary<string, (int Row, int Col)> PositionsAfter(
        string layout, IReadOnlyList<KlotskiMove> moves)
    {
        var parsed = JsonSerializer.Deserialize<KlotskiLayout>(layout, Json)!;
        var positions = parsed.Pieces!.ToDictionary(p => p.Id, p => (p.Row, p.Col));

        foreach (var move in moves)
        {
            var current = positions[move.Id];
            positions[move.Id] = (current.Row + move.Dr, current.Col + move.Dc);
        }

        return positions;
    }
}
