using Gewu.Domain.Games.IdiomCrossword;

namespace Gewu.Domain.Tests.Games.IdiomCrossword;

/// <summary>
/// 相邻不变式 —— 本能力的核心正确性属性。
/// <para>
/// 少了它,生成器会愉快地产出"两条成语平行只隔一格"的网格:并排的字连读起来是无意义的串,
/// 玩家无法把它跟真正的约束区分开。谜题仍然"可解",但已经坏了 —— 而这种坏法不会让任何
/// 别的测试自然失败,所以它值得被单独钉住。
/// </para>
/// </summary>
public class CrosswordGridTests
{
    private static SourceIdiom I(string word) => new(word, $"{word} 的释义。");

    [Fact]
    public void Seed_occupies_its_cells_left_to_right()
    {
        var grid = new CrosswordGrid();
        grid.PlaceSeed(I("合而为一"));

        grid.Cells.Should().HaveCount(4);
        grid.Cells[new CrosswordCell(0, 0)].Should().Be('合');
        grid.Cells[new CrosswordCell(0, 3)].Should().Be('一');
        grid.Words.Should().HaveCount(1);
    }

    [Fact]
    public void Seed_can_only_be_the_first_placement()
    {
        var grid = new CrosswordGrid();
        grid.PlaceSeed(I("合而为一"));

        var act = () => grid.PlaceSeed(I("合情合理"));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_legitimate_crossing_is_accepted()
    {
        var grid = new CrosswordGrid();
        grid.PlaceSeed(I("合而为一"));

        // 合情合理 竖排,首字「合」落在 (0,0) —— 与横排共用那一格。
        var candidate = new PlacedWord(1, I("合情合理"), 0, 0, CrosswordDirection.Vertical);

        grid.CanPlace(candidate, new CrosswordCell(0, 0)).Should().BeTrue();
    }

    [Fact]
    public void A_crossing_whose_characters_disagree_is_rejected()
    {
        var grid = new CrosswordGrid();
        grid.PlaceSeed(I("合而为一"));

        // (0,1) 上是「而」,但候选词在该位置是「情」—— 字不符。
        var candidate = new PlacedWord(1, I("情理之中"), 0, 1, CrosswordDirection.Vertical);

        grid.CanPlace(candidate, new CrosswordCell(0, 1)).Should().BeFalse();
    }

    [Fact]
    public void A_parallel_adjacent_placement_is_rejected()
    {
        var grid = new CrosswordGrid();
        grid.PlaceSeed(I("合而为一"));            // 第 0 行,列 0..3

        // 试图把另一条横排词摆在第 1 行、同样的列范围:它跟第 0 行整排贴着,
        // 每一列都会形成一对没人想要的上下相邻字。
        var candidate = new PlacedWord(1, I("情理之中"), 1, 0, CrosswordDirection.Horizontal);

        grid.CanPlace(candidate, new CrosswordCell(1, 0)).Should().BeFalse();
    }

    [Fact]
    public void A_vertical_word_running_alongside_the_seed_is_rejected()
    {
        var grid = new CrosswordGrid();
        grid.PlaceSeed(I("合而为一"));

        // 竖排词摆在 (1,1) 起 —— 它的首格 (1,1) 与第 0 行的 (0,1) 上下相邻,
        // 但 (1,1) 不是交叉格,所以必须被拒。
        var candidate = new PlacedWord(1, I("情理之中"), 1, 1, CrosswordDirection.Vertical);

        grid.CanPlace(candidate, new CrosswordCell(1, 1)).Should().BeFalse();
    }

    [Fact]
    public void The_intersection_cell_itself_is_exempt_from_the_adjacency_rule()
    {
        var grid = new CrosswordGrid();
        grid.PlaceSeed(I("合而为一"));
        grid.Place(I("合情合理"), 0, 0, CrosswordDirection.Vertical);

        // (0,0) 同时属于两条词,它的邻格 (0,1) 与 (1,0) 都被占用 —— 这正是交叉的样子,
        // 不构成违规。
        grid.WordCountAt(new CrosswordCell(0, 0)).Should().Be(2);
        grid.SatisfiesAdjacencyInvariant().Should().BeTrue();
    }

    [Fact]
    public void A_cell_belonging_to_one_word_is_not_an_intersection()
    {
        var grid = new CrosswordGrid();
        grid.PlaceSeed(I("合而为一"));
        grid.Place(I("合情合理"), 0, 0, CrosswordDirection.Vertical);

        grid.WordCountAt(new CrosswordCell(0, 2)).Should().Be(1);
        grid.WordCountAt(new CrosswordCell(3, 0)).Should().Be(1);
    }

    [Fact]
    public void The_invariant_audit_catches_a_grid_built_past_the_check()
    {
        var grid = new CrosswordGrid();
        grid.PlaceSeed(I("合而为一"));

        // 绕过 CanPlace 直接摆一条平行贴边的词 —— 独立审计必须抓到它。
        // 这是 CanPlace 与 SatisfiesAdjacencyInvariant 刻意的冗余:两条路都得堵。
        grid.Place(I("情理之中"), 1, 0, CrosswordDirection.Horizontal);

        grid.SatisfiesAdjacencyInvariant().Should().BeFalse();
    }
}
