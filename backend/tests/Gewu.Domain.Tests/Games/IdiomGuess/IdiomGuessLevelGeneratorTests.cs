using Gewu.Domain.Games.IdiomGuess;

namespace Gewu.Domain.Tests.Games.IdiomGuess;

/// <summary>
/// 生成器。
/// <para>
/// <b>它的主要工作不是随机,是拒绝</b> —— 被挖的字不得出现在自己的释义里。所以本组的
/// 核心不是「生成出来了」,而是**关掉那条规则必须能生成出反例**:一条永远不触发的
/// 规则和一条正确的规则,产物看起来一模一样。
/// </para>
/// </summary>
public class IdiomGuessLevelGeneratorTests
{
    // 这三条夹具的释义都是**照着判据挑的**,不是随手写的散文。
    //
    // 第一版把「一鸣惊人」当作「只有一个位置可挖」,而它的释义里有「一下子」和「让人」
    // —— 可挖的其实是 {1, 2} 两位。**实现是对的,夹具是错的**,而失败信息看起来
    // 完全像是实现算错了。手写一条断言之前,先照着判据把那句话读一遍。

    /// <summary>四个字全都出现在自己释义里 —— 一个位置都挖不了。全量里这样的有 2,914 条。</summary>
    private static readonly GuessSourceIdiom AllCharactersLeak =
        new("一丝一毫", "一丝一毫都不差,形容极少。", null);

    /// <summary>只有第 3 位(「兔」)不在释义里 —— 守、株、待 三个字都写在里面。</summary>
    private static readonly GuessSourceIdiom OneBlankable =
        new("守株待兔", "比喻不主动努力,死守着株旁等待,而存侥幸心理。", "《韩非子》");

    /// <summary>四个字一个都不在释义里。</summary>
    private static readonly GuessSourceIdiom FullyBlankable =
        new("画蛇添足", "比喻做了多余的事,反而不合适。", "《战国策》");

    // ---- 这条规则本身 ----

    [Fact]
    public void A_character_that_appears_in_its_own_explanation_is_not_blankable()
    {
        IdiomGuessLevelGenerator.BlankablePositions(AllCharactersLeak).Should().BeEmpty(
            "四个字全写在释义里 —— 挖哪个都等于把答案印在题面上");
    }

    [Fact]
    public void The_positions_that_are_absent_from_the_explanation_are_blankable()
    {
        IdiomGuessLevelGenerator.BlankablePositions(OneBlankable).Should().Equal(3);
        IdiomGuessLevelGenerator.BlankablePositions(FullyBlankable).Should().Equal(0, 1, 2, 3);
    }

    // ---- 产物 ----

    /// <summary>
    /// **本组最重要的一条:产物里没有一道题把答案印在题面上。**
    /// </summary>
    [Fact]
    public void No_generated_puzzle_prints_its_answer_in_the_explanation()
    {
        var level = Generate(new GuessDifficultyDial(PuzzleCount: 3, BlankCount: 1, MaxTier: 2));

        AssertNoLeak(level);
    }

    /// <summary>
    /// **正面对照,而它不是可选的。** 上面那条对一个「从不挖任何字」的实现同样是绿的,
    /// 也对一个「那条规则根本没生效」的实现是绿的 —— 只要恰好没抽到会泄题的条目。
    /// <para>
    /// 这里绕过那条规则,直接挖一个出现在释义里的字,然后断言检查器**确实看得出来**。
    /// 检查器看不出来的话,上面那条从来就没验过任何东西。
    /// </para>
    /// </summary>
    [Fact]
    public void The_leak_check_really_catches_a_leak()
    {
        // 手工造一道「把答案印在题面上」的题:挖掉"丝",而"丝"就在释义里。
        var leaked = new GeneratedGuessLevel(
            new IdiomGuessLayout(new[]
            {
                new IdiomGuessPuzzle(0, AllCharactersLeak.Explanation,
                    new string?[] { "一", null, "一", "毫" }),
            }),
            new IdiomGuessSolution(new[] { new IdiomGuessAnswer(0, AllCharactersLeak.Word, null) }),
            1);

        var act = () => AssertNoLeak(leaked);

        act.Should().Throw<Xunit.Sdk.XunitException>(
            "检查器必须看得出来 —— 否则上面那条断言从未验过任何东西");
    }

    [Fact]
    public void Blanked_positions_are_null_and_the_rest_match_the_answer()
    {
        var level = Generate(new GuessDifficultyDial(PuzzleCount: 3, BlankCount: 1, MaxTier: 2));

        foreach (var puzzle in level.Layout.Puzzles)
        {
            var answer = level.Solution.Puzzles.Single(a => a.Index == puzzle.Index);
            puzzle.Chars.Should().HaveCount(4);
            puzzle.Chars.Count(c => c is null).Should().Be(1);
            for (var i = 0; i < 4; i++)
            {
                if (puzzle.Chars[i] is { } shown)
                {
                    shown.Should().Be(answer.Word[i].ToString());
                }
            }
        }
    }

    /// <summary>挖两个字的档位:够挖的条目才进得来。</summary>
    [Fact]
    public void A_two_blank_dial_only_uses_idioms_with_two_blankable_positions()
    {
        var level = Generate(new GuessDifficultyDial(PuzzleCount: 1, BlankCount: 2, MaxTier: 2));

        level.Layout.Puzzles.Should().HaveCount(1);
        level.Layout.Puzzles[0].Chars.Count(c => c is null).Should().Be(2);
        AssertNoLeak(level);
    }

    /// <summary>只挖得动一个字的那条,进不了两空的档位。</summary>
    [Fact]
    public void An_idiom_with_one_blankable_position_cannot_fill_a_two_blank_level()
    {
        var generator = new IdiomGuessLevelGenerator(new[] { OneBlankable }, seed: 1);

        var level = generator.Generate(
            new GuessDifficultyDial(1, BlankCount: 2, MaxTier: 2), 1, new HashSet<string>());

        level.Layout.Puzzles.Should().BeEmpty("凑不满就少放,不能硬放一条会泄题的");
    }

    // ---- 可复现 ----

    [Fact]
    public void The_same_seed_gives_the_same_level()
    {
        var dial = new GuessDifficultyDial(3, 1, 2);

        var a = Generate(dial, seed: 7);
        var b = Generate(dial, seed: 7);

        a.Solution.Puzzles.Select(p => p.Word)
            .Should().Equal(b.Solution.Puzzles.Select(p => p.Word));
    }

    [Fact]
    public void A_level_never_repeats_an_idiom_the_caller_has_already_used()
    {
        var generator = new IdiomGuessLevelGenerator(Corpus(), seed: 3);
        var used = new HashSet<string>(StringComparer.Ordinal);

        var first = generator.Generate(new GuessDifficultyDial(2, 1, 2), 1, used);
        var second = generator.Generate(new GuessDifficultyDial(2, 1, 2), 2, used);

        first.Solution.Puzzles.Select(p => p.Word)
            .Should().NotIntersectWith(second.Solution.Puzzles.Select(p => p.Word));
    }

    // ---- helpers ----

    private static IReadOnlyList<GuessSourceIdiom> Corpus() =>
    [
        OneBlankable,
        FullyBlankable,
        AllCharactersLeak,
        new("对牛弹琴", "比喻对不懂道理的人讲道理。", null),
        new("杯弓蛇影", "比喻疑神疑鬼,自相惊扰。", "《风俗通义》"),
        new("刻舟求剑", "比喻不懂事物已发展变化而仍静止地看问题。", "《吕氏春秋》"),
    ];

    private static GeneratedGuessLevel Generate(GuessDifficultyDial dial, int seed = 42)
        => new IdiomGuessLevelGenerator(Corpus(), seed)
            .Generate(dial, difficulty: 1, new HashSet<string>(StringComparer.Ordinal));

    /// <summary>产物里每一个被挖的字,都不得出现在它自己那条释义里。</summary>
    private static void AssertNoLeak(GeneratedGuessLevel level)
    {
        foreach (var puzzle in level.Layout.Puzzles)
        {
            var answer = level.Solution.Puzzles.Single(a => a.Index == puzzle.Index);
            for (var i = 0; i < puzzle.Chars.Count; i++)
            {
                if (puzzle.Chars[i] is null)
                {
                    puzzle.Explanation.Should().NotContain(
                        answer.Word[i].ToString(),
                        $"「{answer.Word}」第 {i} 位被挖,而那个字就写在释义里");
                }
            }
        }
    }
}
