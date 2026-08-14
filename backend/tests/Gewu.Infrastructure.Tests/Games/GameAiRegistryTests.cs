using Gewu.Domain.Ai;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.TicTacToe;
using Gewu.Infrastructure.Games;

namespace Gewu.Infrastructure.Tests.Games;

/// <summary>
/// AI 工厂注册表。与 <see cref="GameRulesRegistryTests"/> 逐条同构 —— 形状一致的价值就在
/// 于此:读过一个就读懂了其余三个(puzzle 规则 / 棋种规则 / 棋种 AI / 前端游戏目录)。
/// </summary>
public class GameAiRegistryTests
{
    private static GameAiRegistry Registry(params IGameAiFactory[] factories) => new(factories);

    [Fact]
    public void Resolves_both_registered_games()
    {
        var registry = Registry(new GomokuAiFactory(), new TicTacToeAiFactory());

        registry.For(GameKeys.Gomoku).Should().BeOfType<GomokuAiFactory>();
        registry.For(GameKeys.TicTacToe).Should().BeOfType<TicTacToeAiFactory>();
    }

    [Fact]
    public void Returns_null_for_an_unknown_game()
    {
        var registry = Registry(new GomokuAiFactory(), new TicTacToeAiFactory());

        registry.For("xiangqi").Should().BeNull();
    }

    [Fact]
    public void Returns_null_for_an_empty_registry()
    {
        Registry().For(GameKeys.Gomoku).Should().BeNull();
    }

    [Fact]
    public void Keys_are_case_sensitive()
    {
        Registry(new GomokuAiFactory()).For("Gomoku").Should().BeNull();
    }

    [Fact]
    public void A_game_can_have_rules_but_no_AI()
    {
        // 两个注册表分开注册,不是重复 —— 注册单位不同。一个棋种可以先有规则
        // (人人对战可玩),后有 AI。中国象棋大概就会经历这个阶段。
        var rules = new GameRulesRegistry([BuiltInGameRules.Gomoku, BuiltInGameRules.TicTacToe]);
        var ai = Registry(new GomokuAiFactory());

        rules.For(GameKeys.TicTacToe).Should().NotBeNull();
        ai.For(GameKeys.TicTacToe).Should().BeNull();
    }
}
