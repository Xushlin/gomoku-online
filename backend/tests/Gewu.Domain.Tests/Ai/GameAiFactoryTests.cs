using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.TicTacToe;

namespace Gewu.Domain.Tests.Ai;

/// <summary>
/// 两个棋种的 AI 工厂。
/// <para>
/// 一字棋那组断言里最要紧的一条是 <c>Easy</c> 返回 <see cref="EasyAi"/> ——
/// 复用了五子棋的实现,一行都没改。另两档是它的反证:各自都得重写。
/// </para>
/// </summary>
public class GameAiFactoryTests
{
    private static readonly IGameAiFactory Gomoku = new GomokuAiFactory();
    private static readonly IGameAiFactory TicTacToe = new TicTacToeAiFactory();

    // ---- 棋种键 ----

    [Fact]
    public void Each_factory_declares_its_game()
    {
        Gomoku.GameKey.Should().Be(GameKeys.Gomoku);
        TicTacToe.GameKey.Should().Be(GameKeys.TicTacToe);
    }

    // ---- 五子棋:分支一字未改 ----

    [Fact]
    public void Easy_Branch_Returns_EasyAi()
    {
        Gomoku.Create(BotDifficulty.Easy, new Random(1)).Should().BeOfType<EasyAi>();
    }

    [Fact]
    public void Medium_Branch_Returns_MediumAi()
    {
        Gomoku.Create(BotDifficulty.Medium, new Random(1)).Should().BeOfType<MediumAi>();
    }

    [Fact]
    public void Hard_Branch_Returns_HardAi()
    {
        Gomoku.Create(BotDifficulty.Hard, new Random(1)).Should().BeOfType<HardAi>();
    }

    [Fact]
    public void Undefined_Difficulty_Throws()
    {
        var act = () => Gomoku.Create((BotDifficulty)99, new Random(1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Null_Random_Throws()
    {
        var act = () => Gomoku.Create(BotDifficulty.Easy, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ---- 一字棋 ----

    [Fact]
    public void TicTacToe_Easy_reuses_the_gomoku_implementation()
    {
        // 这条断言是本变更最想留下的一句证据:EasyAi 只按 board.Rows / board.Cols 遍历,
        // 不含任何棋种假设,所以第二个棋种直接拿来用。若哪天有人写了 TicTacToeEasyAi,
        // 这条测试会挂 —— 那时该问的是"EasyAi 哪里不够用",而不是顺手改掉断言。
        TicTacToe.Create(BotDifficulty.Easy, new Random(1)).Should().BeOfType<EasyAi>();
    }

    [Fact]
    public void TicTacToe_Medium_and_Hard_are_their_own_implementations()
    {
        TicTacToe.Create(BotDifficulty.Medium, new Random(1))
            .Should().BeOfType<TicTacToeMediumAi>();
        TicTacToe.Create(BotDifficulty.Hard, new Random(1))
            .Should().BeOfType<TicTacToeHardAi>();
    }

    [Fact]
    public void TicTacToe_Undefined_Difficulty_Throws()
    {
        var act = () => TicTacToe.Create((BotDifficulty)99, new Random(1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TicTacToe_Null_Random_Throws()
    {
        // Hard 用不到随机源,但工厂仍然拒绝 null —— 契约不该因为某一档恰好不需要而松掉。
        var act = () => TicTacToe.Create(BotDifficulty.Hard, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Every_call_returns_a_fresh_instance()
    {
        // 工厂是 singleton,被并发的多个房间共享;交出同一个 AI 实例就等于共享它的随机源。
        var first = TicTacToe.Create(BotDifficulty.Medium, new Random(1));
        var second = TicTacToe.Create(BotDifficulty.Medium, new Random(1));

        first.Should().NotBeSameAs(second);
    }
}
