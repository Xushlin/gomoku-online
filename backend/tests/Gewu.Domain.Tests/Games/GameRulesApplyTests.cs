using System.Reflection;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games;

/// <summary>
/// <c>IGameRules.Apply</c> —— 走子合法性与胜负判定的唯一入口。
/// <para>
/// 这些用例盯的是**抽象本身**,不是五子棋:聚合根现在只验房间态 / 玩家 / 回合,
/// 越界、重复落子、走法形状全部下沉到了这里。中国象棋进这个聚合的前提就是这条边界成立。
/// </para>
/// </summary>
public class GameRulesApplyTests
{
    private static readonly INInARowRules Gomoku = BuiltInGameRules.Gomoku;
    private static readonly INInARowRules TicTacToe = BuiltInGameRules.TicTacToe;

    private static readonly IReadOnlyList<PlayedMove> Empty = [];

    private static PlayedMove Placed(int row, int col, Stone side)
        => PlayedMove.Positional(null, new Position(row, col), side);

    // ---- 合法路径 ----

    [Fact]
    public void A_legal_placement_on_an_empty_board_is_ongoing()
    {
        var result = Gomoku.Apply(Empty, MoveIntent.Place(new Position(7, 7)), Stone.Black);

        result.Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void Five_in_a_row_ends_the_game()
    {
        // 黑方已连四,第五子成五。
        var history = new List<PlayedMove>
        {
            Placed(7, 3, Stone.Black), Placed(0, 0, Stone.White),
            Placed(7, 4, Stone.Black), Placed(0, 1, Stone.White),
            Placed(7, 5, Stone.Black), Placed(0, 2, Stone.White),
            Placed(7, 6, Stone.Black), Placed(0, 3, Stone.White),
        };

        var result = Gomoku.Apply(history, MoveIntent.Place(new Position(7, 7)), Stone.Black);

        result.Result.Should().Be(GameResult.BlackWin);
    }

    [Fact]
    public void A_full_tictactoe_board_with_no_line_is_a_draw()
    {
        //   X O X
        //   X O O
        //   O X _     黑走 (2,2) 填满,无三连 → 和棋。
        var history = new List<PlayedMove>
        {
            Placed(0, 0, Stone.Black), Placed(0, 1, Stone.White),
            Placed(0, 2, Stone.Black), Placed(1, 1, Stone.White),
            Placed(1, 0, Stone.Black), Placed(1, 2, Stone.White),
            Placed(2, 1, Stone.Black), Placed(2, 0, Stone.White),
        };

        var result = TicTacToe.Apply(history, MoveIntent.Place(new Position(2, 2)), Stone.Black);

        result.Result.Should().Be(GameResult.Draw);
    }

    // ---- 规则自己拒绝非法走子 ----

    [Fact]
    public void Out_of_bounds_is_rejected_by_the_rules()
    {
        var act = () => TicTacToe.Apply(Empty, MoveIntent.Place(new Position(3, 0)), Stone.Black);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void An_occupied_square_is_rejected_by_the_rules()
    {
        var history = new List<PlayedMove> { Placed(0, 0, Stone.Black) };

        var act = () => TicTacToe.Apply(history, MoveIntent.Place(new Position(0, 0)), Stone.White);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void An_empty_side_is_rejected()
    {
        var act = () => Gomoku.Apply(Empty, MoveIntent.Place(new Position(0, 0)), Stone.Empty);

        act.Should().Throw<InvalidMoveException>();
    }

    // ---- 形状校验属于规则 ----

    [Fact]
    public void A_placement_game_rejects_a_move_that_carries_an_origin()
    {
        // 五子棋是落子类:一步棋只有落点。带起点的载荷不是「走错了」,
        // 是「客户端发了一个这个棋种不存在的走法」。这条判断在规则里,不在聚合根里 ——
        // 聚合根不知道哪些棋种走子。
        var act = () => Gomoku.Apply(
            Empty, MoveIntent.Slide(new Position(0, 0), new Position(1, 1)), Stone.Black);

        act.Should().Throw<InvalidMoveException>()
            .WithMessage("*origin*");
    }

    [Fact]
    public void The_bounds_check_belongs_to_the_game_not_the_coordinate()
    {
        // 同一个坐标,一个棋种界内、另一个界外。Position 只保证非负。
        var p = new Position(5, 5);

        Gomoku.Invoking(r => r.Apply(Empty, MoveIntent.Place(p), Stone.Black))
            .Should().NotThrow();
        TicTacToe.Invoking(r => r.Apply(Empty, MoveIntent.Place(p), Stone.Black))
            .Should().Throw<InvalidMoveException>();
    }

    // ---- 无状态 ----

    [Fact]
    public void The_same_instance_serving_two_histories_does_not_mix_them()
    {
        // 规则实例被并发的多个房间共享。任何随对局变化的字段都会变成跨房间的串味,
        // 而那种 bug 在单线程测试里看不见 —— 这条至少钉住「结果只取决于入参」。
        var far = new List<PlayedMove> { Placed(0, 0, Stone.Black) };

        var a = Gomoku.Apply(far, MoveIntent.Place(new Position(7, 7)), Stone.White);
        var b = Gomoku.Apply(Empty, MoveIntent.Place(new Position(7, 7)), Stone.White);
        var c = Gomoku.Apply(far, MoveIntent.Place(new Position(7, 7)), Stone.White);

        a.Should().Be(b);
        b.Should().Be(c);

        // 而且 (0,0) 在第二次调用里是空的 —— 历史没有泄漏进来。
        Gomoku.Invoking(r => r.Apply(Empty, MoveIntent.Place(new Position(0, 0)), Stone.White))
            .Should().NotThrow();
    }

    [Fact]
    public void Apply_does_not_mutate_the_history_it_is_given()
    {
        var history = new List<PlayedMove> { Placed(0, 0, Stone.Black) };

        Gomoku.Apply(history, MoveIntent.Place(new Position(7, 7)), Stone.White);

        history.Should().ContainSingle();
    }

    // ---- 接口只承载对每个实现都成立的东西 ----

    [Fact]
    public void The_base_interface_carries_no_n_in_a_row_concepts()
    {
        // 中国象棋没有「连几子」,CreateBoard 返回的 Board 它也不用。留在基接口上,
        // 象棋就得实现两个骗人的成员 —— 而骗人的实现是下一个人删不掉的(他无从知道有没有调用方)。
        var members = typeof(IGameRules)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToList();

        members.Should().NotContain("WinLength");
        members.Should().NotContain("CreateBoard");
        members.Should().NotContain("ReplayBoard");
        members.Should().Contain("Apply");
    }

    [Fact]
    public void The_narrow_interface_still_exposes_them()
    {
        Gomoku.WinLength.Should().Be(5);
        Gomoku.CreateBoard().Rows.Should().Be(15);
        Gomoku.ReplayBoard([]).Rows.Should().Be(15);
    }

    [Fact]
    public void ReplayBoard_rebuilds_the_position_from_history()
    {
        var history = new List<PlayedMove>
        {
            Placed(7, 7, Stone.Black), Placed(8, 8, Stone.White), Placed(7, 8, Stone.Black),
        };

        var board = Gomoku.ReplayBoard(history);

        board.GetStone(new Position(7, 7)).Should().Be(Stone.Black);
        board.GetStone(new Position(8, 8)).Should().Be(Stone.White);
        board.GetStone(new Position(7, 8)).Should().Be(Stone.Black);
        board.GetStone(new Position(7, 9)).Should().Be(Stone.Empty);
    }
}
