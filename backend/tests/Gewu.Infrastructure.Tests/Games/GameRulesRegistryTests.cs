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
        rules!.Rows.Should().Be(15);
        rules.WinLength.Should().Be(5);
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
        // 一字棋将来的全部代价:一行注册,连规则类都不用写。
        var ticTacToe = new NInARowRules("tictactoe", 3, 3, 3);
        var registry = Registry(BuiltInGameRules.Gomoku, ticTacToe);

        registry.For("gomoku")!.Rows.Should().Be(15);
        registry.For("tictactoe")!.Rows.Should().Be(3);
    }
}
