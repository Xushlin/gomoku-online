using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.Xiangqi;

/// <summary>
/// **`generalize-match-domain` 的验收条件,现在才第一次真正被检验:
/// 加象棋不需要碰 `Room` / `Game` / `Move`。**
/// <para>
/// 这些用例用真的聚合根走真的象棋 —— 不是对着 `XiangqiRules` 单测走法,而是走完整条路径:
/// 房间态 → 身份 → 回合 → 规则 → 记录 → 结束。哪一条需要给象棋开特例,
/// 就说明上一次抽象抽错了地方。
/// </para>
/// </summary>
public class XiangqiThroughRoomTests
{
    private static readonly DateTime Now = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IGameRules Rules = BuiltInGameRules.Xiangqi;

    /// <summary>红方 —— 先手,占 <c>BlackPlayerId</c> 这个座位。</summary>
    private static readonly int Red = BoardSeats.FirstSeat;

    private static (Room Room, UserId RedPlayer, UserId BlackPlayer) PlayingRoom()
    {
        var red = UserId.NewId();
        var black = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "xiangqi room", red, Now, GameKeys.Xiangqi);
        room.JoinAsPlayer(black, Now.AddSeconds(1), BuiltInGameRules.Gomoku);
        return (room, red, black);
    }

    private static MoveOutcome Play(
        Room room, UserId who, int fr, int fc, int tr, int tc, int atSecond)
        => room.PlayMove(
            who,
            MoveIntent.Slide(new Position(fr, fc), new Position(tr, tc)),
            Now.AddSeconds(atSecond),
            Rules);

    [Fact]
    public void Red_moves_first_because_Stone_Black_is_red()
    {
        // Game 初始化 CurrentTurn = Stone.Black,而象棋红先 —— 这正是把红方读作
        // Stone.Black 的理由。Domain 因此一行都不用改。
        var (room, red, _) = PlayingRoom();

        room.Game!.CurrentTurn.Should().Be(Red);
        Play(room, red, 6, 0, 5, 0, 2).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void The_black_side_cannot_open()
    {
        var (room, _, black) = PlayingRoom();

        var act = () => Play(room, black, 3, 0, 4, 0, 2);

        act.Should().Throw<NotYourTurnException>();
    }

    [Fact]
    public void A_slide_move_stores_its_origin()
    {
        var (room, red, _) = PlayingRoom();

        Play(room, red, 9, 1, 7, 2, 2);

        var move = room.Game!.Moves.Single();
        move.FromRow.Should().Be(9);
        move.FromCol.Should().Be(1);
        move.Row.Should().Be(7);
        move.Col.Should().Be(2);
        move.Seat.Should().Be(Red);
    }

    [Fact]
    public void The_history_the_rules_see_round_trips_through_the_aggregate()
    {
        var (room, red, black) = PlayingRoom();

        Play(room, red, 6, 0, 5, 0, 2);
        Play(room, black, 3, 0, 4, 0, 3);
        Play(room, red, 9, 1, 7, 2, 4);

        room.Game!.History().Should().Equal(
            PlayedMove.Positional(new Position(6, 0), new Position(5, 0), BoardSeats.FirstSeat),
            PlayedMove.Positional(new Position(3, 0), new Position(4, 0), BoardSeats.SecondSeat),
            PlayedMove.Positional(new Position(9, 1), new Position(7, 2), BoardSeats.FirstSeat));
    }

    [Fact]
    public void Turns_alternate_across_a_dozen_real_moves()
    {
        // 一段真实的开局。走完之后回合、步数、起点都对得上,聚合根没有为象棋开任何特例。
        var (room, red, black) = PlayingRoom();

        (int Fr, int Fc, int Tr, int Tc)[] opening =
        [
            (7, 1, 7, 4),  // 红炮二平五
            (0, 1, 2, 2),  // 黑马
            (9, 1, 7, 2),  // 红马
            (0, 7, 2, 6),  // 黑马
            (9, 0, 8, 0),  // 红车
            (3, 2, 4, 2),  // 黑卒
            (8, 0, 8, 4),  // 红车横移
            (0, 0, 1, 0),  // 黑车
            (6, 6, 5, 6),  // 红兵
            (1, 0, 1, 4),  // 黑车横移
        ];

        var second = 2;
        for (var i = 0; i < opening.Length; i++)
        {
            var (fr, fc, tr, tc) = opening[i];
            var mover = i % 2 == 0 ? red : black;
            Play(room, mover, fr, fc, tr, tc, second++).Result.Should().Be(GameResult.Ongoing);
        }

        room.Game!.Moves.Should().HaveCount(opening.Length);
        room.Game.Moves.Should().OnlyContain(m => m.FromRow != null && m.FromCol != null);
        room.Status.Should().Be(RoomStatus.Playing);
    }

    [Fact]
    public void An_illegal_move_leaves_the_aggregate_untouched()
    {
        var (room, red, _) = PlayingRoom();

        // 红车 (9,0) 被自家兵 (6,0) 挡着,走不到 (4,0)。
        var act = () => Play(room, red, 9, 0, 4, 0, 2);

        act.Should().Throw<InvalidMoveException>();
        room.Game!.Moves.Should().BeEmpty();
        room.Game.CurrentTurn.Should().Be(Red);
        room.Status.Should().Be(RoomStatus.Playing);
    }

    [Fact]
    public void A_placement_style_move_is_rejected_for_xiangqi()
    {
        // 聚合根照转不误 —— 是**规则**说象棋没有「落子」。
        var (room, red, _) = PlayingRoom();

        var act = () => room.PlayMove(
            red, MoveIntent.Place(new Position(5, 0)), Now.AddSeconds(2), Rules);

        act.Should().Throw<InvalidMoveException>().WithMessage("*origin*");
        room.Game!.Moves.Should().BeEmpty();
    }

    [Fact]
    public void Resigning_a_xiangqi_game_works_unchanged()
    {
        // 认输与超时两条结束路径完全不经过规则 —— 象棋不该需要它们改动。
        var (room, red, black) = PlayingRoom();
        Play(room, red, 6, 0, 5, 0, 2);

        var ended = room.Resign(red, Now.AddSeconds(3));

        ended.Result.Should().Be(GameResult.Decided);
        ended.WinnerUserId.Should().Be(black, "认输之后赢的是对手");
        room.Game!.EndReason.Should().Be(GameEndReason.Resigned);
        room.Status.Should().Be(RoomStatus.Finished);
    }
}
