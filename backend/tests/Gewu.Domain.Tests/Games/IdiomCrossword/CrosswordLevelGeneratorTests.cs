using System.Text.Json;
using Gewu.Domain.Games.IdiomCrossword;

namespace Gewu.Domain.Tests.Games.IdiomCrossword;

/// <summary>
/// 生成器的确定性与产出合法性。
/// <para>
/// 确定性不是洁癖:它是"关卡集是可追溯的产物"而不是"某台机器上发生过一次的事件"的全部
/// 依据。坏关卡能被复现才能被修,重新生成的 diff 才干净,评审者才能自己重跑一遍确认
/// 提交的文件就是工具产出的那个。
/// </para>
/// </summary>
public class CrosswordLevelGeneratorTests
{
    /// <summary>
    /// 一个小语料,交叉字足够多,能稳定长出多条成语。
    /// </summary>
    private static readonly SourceIdiom[] Corpus =
    {
        new("合而为一", "合成一个整体。"),
        new("合情合理", "合乎情理。"),
        new("一心一意", "形容做事专心。"),
        new("心想事成", "心里想的都能实现。"),
        new("一往无前", "一直往前,无所阻挡。"),
        new("成家立业", "组建家庭,建立事业。"),
        new("大有文章", "话里有很多含意。"),
        new("分文不取", "一个钱也不要。"),
        new("大名鼎鼎", "形容名气很大。"),
        new("立身扬名", "立足于世并使名声远扬。"),
        new("如出一口", "许多人说的话完全一致。"),
        new("心口不一", "心里想的和嘴上说的不一样。"),
        new("小家子气", "行事不大方。"),
        new("声求气应", "志趣相同的人自然结合。"),
        new("口出大言", "说大话。"),
    };

    private static ISet<string> Dictionary()
        => Corpus.Select(i => i.Word).ToHashSet(StringComparer.Ordinal);

    private static readonly DifficultyDial Dial = new(IdiomCount: 4, GivenCount: 2, DistractorCount: 2);

    private static string Serialize(GeneratedLevel level)
        => JsonSerializer.Serialize(new { level.Layout, level.Solution, level.Difficulty });

    [Fact]
    public void The_same_seed_produces_byte_identical_output()
    {
        var first = new CrosswordLevelGenerator(Corpus, seed: 4242).Generate(Dial, difficulty: 1);
        var second = new CrosswordLevelGenerator(Corpus, seed: 4242).Generate(Dial, difficulty: 1);

        Serialize(first).Should().Be(Serialize(second));
    }

    [Fact]
    public void Different_seeds_produce_different_levels()
    {
        var a = new CrosswordLevelGenerator(Corpus, seed: 1).Generate(Dial, difficulty: 1);
        var b = new CrosswordLevelGenerator(Corpus, seed: 999).Generate(Dial, difficulty: 1);

        Serialize(a).Should().NotBe(Serialize(b));
    }

    [Fact]
    public void Corpus_order_does_not_change_the_output()
    {
        // 生成器在建索引前先排序,所以调用方给的顺序不影响产物 —— 否则"同种子同词典
        // ⇒ 同产物"会被一次无关的重排悄悄打破。
        var shuffled = Corpus.Reverse().ToList();

        var fromOriginal = new CrosswordLevelGenerator(Corpus, seed: 77).Generate(Dial, difficulty: 1);
        var fromShuffled = new CrosswordLevelGenerator(shuffled, seed: 77).Generate(Dial, difficulty: 1);

        Serialize(fromOriginal).Should().Be(Serialize(fromShuffled));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Every_generated_level_passes_audit(int seed)
    {
        var level = new CrosswordLevelGenerator(Corpus, seed).Generate(Dial, difficulty: 1);

        var audit = CrosswordAudit.Check(level, Dictionary());

        audit.Failures.Should().BeEmpty();
        audit.Passed.Should().BeTrue();
    }

    [Fact]
    public void A_generated_level_interlocks_at_least_two_idioms()
    {
        var level = new CrosswordLevelGenerator(Corpus, seed: 31).Generate(Dial, difficulty: 1);

        level.Layout.Slots.Count.Should().BeGreaterThanOrEqualTo(2);
        level.Solution.Words.Should().HaveCount(level.Layout.Slots.Count);
    }

    [Fact]
    public void The_layout_never_carries_a_full_idiom_or_an_explanation()
    {
        var level = new CrosswordLevelGenerator(Corpus, seed: 8).Generate(Dial, difficulty: 1);

        var layoutJson = JsonSerializer.Serialize(level.Layout);

        foreach (var word in level.Solution.Words)
        {
            layoutJson.Should().NotContain(word.Word, "布局里出现完整成语就是泄题");
            layoutJson.Should().NotContain(word.Explanation);
        }
    }

    [Fact]
    public void The_tray_covers_every_non_given_cell_as_a_multiset()
    {
        // 「一心一意」要两个「一」;字盘只给一个就无解。所以比的是多重集合,不是去重集合。
        var level = new CrosswordLevelGenerator(Corpus, seed: 12).Generate(Dial, difficulty: 1);

        var givenKeys = level.Layout.Given
            .Select(g => CrosswordSolution.Key(g.Row, g.Col))
            .ToHashSet(StringComparer.Ordinal);

        var required = level.Solution.Cells
            .Where(kv => !givenKeys.Contains(kv.Key))
            .GroupBy(kv => kv.Value)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var available = level.Layout.Tray
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        foreach (var (ch, count) in required)
        {
            available.GetValueOrDefault(ch).Should().BeGreaterThanOrEqualTo(count);
        }
    }

    [Fact]
    public void Given_cells_show_the_correct_character()
    {
        var level = new CrosswordLevelGenerator(Corpus, seed: 5).Generate(Dial, difficulty: 1);

        foreach (var given in level.Layout.Given)
        {
            level.Solution.CharAt(new CrosswordCell(given.Row, given.Col))
                .Should().Be(given.Char);
        }
    }

    [Fact]
    public void The_grid_is_normalised_to_the_origin()
    {
        var level = new CrosswordLevelGenerator(Corpus, seed: 64).Generate(Dial, difficulty: 1);

        level.Layout.Cells.Min(c => c.Row).Should().Be(0);
        level.Layout.Cells.Min(c => c.Col).Should().Be(0);
        level.Layout.Cells.Max(c => c.Row).Should().Be(level.Layout.Rows - 1);
        level.Layout.Cells.Max(c => c.Col).Should().Be(level.Layout.Cols - 1);
    }

    [Fact]
    public void No_idiom_appears_twice_in_one_level()
    {
        // 重复会让"答对一条"的反馈含义不明:两个槽同一条成语,玩家不知道点亮了哪个。
        var level = new CrosswordLevelGenerator(Corpus, seed: 21).Generate(Dial, difficulty: 1);

        level.Solution.Words.Select(w => w.Word).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void A_tiny_corpus_yields_fewer_idioms_rather_than_hanging()
    {
        // 摆放预算用尽就少放一条并返回 —— 少放是可以的,空转不行。
        var tiny = new[] { new SourceIdiom("合而为一", "合成一个整体。") };

        var level = new CrosswordLevelGenerator(tiny, seed: 1)
            .Generate(new DifficultyDial(6, 1, 0), difficulty: 1);

        level.Layout.Slots.Should().HaveCount(1);
    }
}
