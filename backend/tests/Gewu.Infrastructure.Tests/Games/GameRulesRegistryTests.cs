using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Infrastructure.Games;

namespace Gewu.Infrastructure.Tests.Games;

/// <summary>
/// 棋种规则注册表。形状与 <c>PuzzleRulesRegistry</c> 一致:按键解析,未知键返回
/// <c>null</c>,由 handler 映射成 404。
/// </summary>
public class GameRulesRegistryTests
{
    private static GameRulesRegistry Registry(params IGameRules[] rules) => new(rules);

    [Fact]
    public void Resolves_a_registered_game()
    {
        var registry = Registry(BuiltInGameRules.Gomoku);

        var rules = registry.For("gomoku");

        rules.Should().NotBeNull();
        rules.Should().BeAssignableTo<IBoardGameRules>().Which.Rows.Should().Be(15);
        rules.Should().BeAssignableTo<INInARowRules>()
            .Which.WinLength.Should().Be(5);
    }

    [Fact]
    public void Returns_null_for_an_unknown_game()
    {
        var registry = Registry(BuiltInGameRules.Gomoku);

        registry.For("xiangqi").Should().BeNull();
    }

    [Fact]
    public void Returns_null_for_an_empty_registry()
    {
        Registry().For("gomoku").Should().BeNull();
    }

    [Fact]
    public void Keys_are_case_sensitive()
    {
        // 大小写不敏感会让 "Gomoku" 和 "gomoku" 变成同一个房间键的两种写法,
        // 而数据库里存的是原样字符串 —— 宁可解析不出来,也不要两种拼法都能用。
        var registry = Registry(BuiltInGameRules.Gomoku);

        registry.For("Gomoku").Should().BeNull();
    }

    [Fact]
    public void Adding_a_game_is_one_more_registration()
    {
        // 一字棋的全部代价:一行注册,连规则类都不用写。
        var registry = Registry(BuiltInGameRules.Gomoku, BuiltInGameRules.TicTacToe);

        registry.For("gomoku").Should().BeAssignableTo<IBoardGameRules>().Which.Rows.Should().Be(15);
        registry.For("tictactoe").Should().BeAssignableTo<IBoardGameRules>().Which.Rows.Should().Be(3);
    }

    [Fact]
    public void Resolves_tictactoe_with_its_registered_parameters()
    {
        var registry = Registry(BuiltInGameRules.Gomoku, BuiltInGameRules.TicTacToe);

        var rules = registry.For("tictactoe");

        rules.Should().NotBeNull();
        var board = rules.Should().BeAssignableTo<IBoardGameRules>().Which;
        board.Rows.Should().Be(3);
        board.Cols.Should().Be(3);
        rules.Should().BeAssignableTo<INInARowRules>()
            .Which.WinLength.Should().Be(3);
        rules.IsRated.Should().BeFalse();
    }

    [Fact]
    public void An_unregistered_game_stays_unresolvable()
    {
        // 一字棋落地之后 "For 返回 null" 这条路径仍然必须存在 —— 落子 handler
        // 的 404 分支全靠它,而那是"房间指向本构建不认识的棋种"的唯一出口。
        var registry = Registry(BuiltInGameRules.Gomoku, BuiltInGameRules.TicTacToe);

        registry.For("xiangqi").Should().BeNull();
    }
}
