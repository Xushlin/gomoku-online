using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.ValueObjects;
using DomainMove = Gewu.Domain.ValueObjects.Move;

namespace Gewu.Domain.Tests.Games;

/// <summary>
/// 「连 N 子」规则的参数化行为。
/// <para>
/// 这里最要紧的一组断言是"同一串子在不同棋种下判定不同" —— 那是本变更的全部意义:
/// 连子长度与盘面尺寸是棋种属性,不是常量。
/// </para>
/// </summary>
public class NInARowRulesTests
{
    // 用真正注册给一字棋的那套规则,而不是在测试里另 new 一个 (3,3,3)。
    // 后者会在注册的参数被改掉时仍然全绿 —— 那正是这组测试要抓的东西。
    private static readonly IGameRules TicTacToe = BuiltInGameRules.TicTacToe;

    // ---- 构造校验 ----

    [Fact]
    public void Gomoku_is_15_by_15_win_5()
    {
        BuiltInGameRules.Gomoku.GameKey.Should().Be("gomoku");
        BuiltInGameRules.Gomoku.Rows.Should().Be(15);
        BuiltInGameRules.Gomoku.Cols.Should().Be(15);
        BuiltInGameRules.Gomoku.WinLength.Should().Be(5);
    }

    [Fact]
    public void TicTacToe_is_3_by_3_win_3()
    {
        TicTacToe.GameKey.Should().Be("tictactoe");
        TicTacToe.Rows.Should().Be(3);
        TicTacToe.Cols.Should().Be(3);
        TicTacToe.WinLength.Should().Be(3);
    }

    [Fact]
    public void Gomoku_is_rated_and_tictactoe_is_not()
    {
        // 一字棋不计分是刻意的、限期的 —— 见 add-tictactoe design D2。
        // add-per-game-rating 删掉 IsRated 时,这条测试会跟着被删。
        BuiltInGameRules.Gomoku.IsRated.Should().BeTrue();
        TicTacToe.IsRated.Should().BeFalse();
    }

    [Fact]
    public void A_game_is_rated_unless_it_says_otherwise()
    {
        new NInARowRules("some-new-game", 3, 3, 3).IsRated.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 3, 3)]
    [InlineData(3, 0, 3)]
    [InlineData(3, 3, 0)]
    [InlineData(-1, 3, 3)]
    public void Rejects_non_positive_dimensions(int rows, int cols, int winLength)
    {
        var act = () => new NInARowRules("bad", rows, cols, winLength);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rejects_a_win_length_nobody_could_ever_reach()
    {
        // 6 子连线放不进 3×5 的盘 —— 这不是一盘难下的棋,是一处配置错误。
        var act = () => new NInARowRules("unwinnable", 3, 5, 6);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rejects_an_empty_game_key()
    {
        var act = () => new NInARowRules("  ", 3, 3, 3);

        act.Should().Throw<ArgumentException>();
    }

    // ---- 边界 ----

    [Fact]
    public void Bounds_follow_the_game_not_the_coordinate()
    {
        var p = new Position(5, 5);

        BuiltInGameRules.Gomoku.IsInBounds(p).Should().BeTrue();
        TicTacToe.IsInBounds(p).Should().BeFalse();
    }

    [Fact]
    public void The_last_cell_is_in_bounds_and_the_next_is_not()
    {
        TicTacToe.IsInBounds(new Position(2, 2)).Should().BeTrue();
        TicTacToe.IsInBounds(new Position(3, 2)).Should().BeFalse();
        TicTacToe.IsInBounds(new Position(2, 3)).Should().BeFalse();
    }

    // ---- 判胜随棋种 ----

    [Fact]
    public void Three_in_a_row_wins_on_a_3x3_board()
    {
        var board = TicTacToe.CreateBoard();

        board.PlaceStone(new DomainMove(new Position(1, 0), Stone.Black));
        board.PlaceStone(new DomainMove(new Position(1, 1), Stone.Black));
        var result = board.PlaceStone(new DomainMove(new Position(1, 2), Stone.Black));

        result.Should().Be(GameResult.BlackWin);
    }

    [Fact]
    public void The_same_three_in_a_row_is_still_ongoing_on_a_gomoku_board()
    {
        // 本变更的核心断言:同样三颗子,换个棋种就不是胜负。
        var board = BuiltInGameRules.Gomoku.CreateBoard();

        board.PlaceStone(new DomainMove(new Position(1, 0), Stone.Black));
        board.PlaceStone(new DomainMove(new Position(1, 1), Stone.Black));
        var result = board.PlaceStone(new DomainMove(new Position(1, 2), Stone.Black));

        result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void Two_in_a_row_does_not_win_on_a_3x3_board()
    {
        var board = TicTacToe.CreateBoard();

        board.PlaceStone(new DomainMove(new Position(0, 0), Stone.White));
        var result = board.PlaceStone(new DomainMove(new Position(0, 1), Stone.White));

        result.Should().Be(GameResult.Ongoing);
    }

    [Theory]
    [InlineData(0, 0, 1, 1, 2, 2)] // ↘
    [InlineData(0, 2, 1, 1, 2, 0)] // ↗
    [InlineData(0, 0, 1, 0, 2, 0)] // 竖
    public void Three_in_a_row_wins_in_every_direction(
        int r1, int c1, int r2, int c2, int r3, int c3)
    {
        var board = TicTacToe.CreateBoard();

        board.PlaceStone(new DomainMove(new Position(r1, c1), Stone.White));
        board.PlaceStone(new DomainMove(new Position(r2, c2), Stone.White));
        var result = board.PlaceStone(new DomainMove(new Position(r3, c3), Stone.White));

        result.Should().Be(GameResult.WhiteWin);
    }

    [Fact]
    public void A_full_3x3_board_with_no_line_is_a_draw()
    {
        // X O X
        // X O O
        // O X X
        var board = TicTacToe.CreateBoard();
        var layout = new[]
        {
            (0, 0, Stone.Black), (0, 1, Stone.White), (0, 2, Stone.Black),
            (1, 0, Stone.Black), (1, 1, Stone.White), (1, 2, Stone.White),
            (2, 0, Stone.White), (2, 1, Stone.Black),
        };

        foreach (var (r, c, stone) in layout)
        {
            board.PlaceStone(new DomainMove(new Position(r, c), stone)).Should().Be(GameResult.Ongoing);
        }

        var last = board.PlaceStone(new DomainMove(new Position(2, 2), Stone.Black));

        last.Should().Be(GameResult.Draw);
    }

    // ---- 非方形棋盘 ----

    [Fact]
    public void A_non_square_board_indexes_by_columns()
    {
        // 3 行 5 列。若 IndexOf 还在用"边长",(2,4) 会算到界外或串行。
        var rules = new NInARowRules("wide", 3, 5, 3);
        var board = rules.CreateBoard();

        rules.IsInBounds(new Position(2, 4)).Should().BeTrue();
        rules.IsInBounds(new Position(3, 0)).Should().BeFalse();

        board.PlaceStone(new DomainMove(new Position(2, 4), Stone.Black));

        board.GetStone(new Position(2, 4)).Should().Be(Stone.Black);
        board.GetStone(new Position(0, 0)).Should().Be(Stone.Empty);
        board.GetStone(new Position(1, 4)).Should().Be(Stone.Empty);
    }

    [Fact]
    public void Clone_preserves_dimensions()
    {
        var board = TicTacToe.CreateBoard();
        board.PlaceStone(new DomainMove(new Position(1, 1), Stone.Black));

        var clone = board.Clone();

        clone.Rows.Should().Be(3);
        clone.Cols.Should().Be(3);
        clone.WinLength.Should().Be(3);
        clone.GetStone(new Position(1, 1)).Should().Be(Stone.Black);

        clone.PlaceStone(new DomainMove(new Position(0, 0), Stone.White));
        board.GetStone(new Position(0, 0)).Should().Be(Stone.Empty);
    }

    [Fact]
    public void Each_CreateBoard_call_returns_a_fresh_board()
    {
        // 规则实例被并发的多个房间共享,所以它绝不能交出同一块盘。
        var first = TicTacToe.CreateBoard();
        first.PlaceStone(new DomainMove(new Position(0, 0), Stone.Black));

        var second = TicTacToe.CreateBoard();

        second.GetStone(new Position(0, 0)).Should().Be(Stone.Empty);
    }
}
