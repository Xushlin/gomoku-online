using Gomoku.Domain.Enums;
using Gomoku.Domain.Exceptions;
using Gomoku.Domain.ValueObjects;

namespace Gomoku.Domain.Tests.Rooms;

public class RoomPlayMoveTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private static Room PlayingRoom(out UserId black, out UserId white)
    {
        black = UserId.NewId();
        white = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "Match", black, Now);
        room.JoinAsPlayer(white, Now.AddMinutes(1));
        return room;
    }

    [Fact]
    public void Legal_Move_Advances_Turn_And_Appends_Move()
    {
        var room = PlayingRoom(out var black, out _);

        var outcome = room.PlayMove(black, new Position(7, 7), Now.AddMinutes(2));

        outcome.Result.Should().Be(GameResult.Ongoing);
        outcome.Move.Ply.Should().Be(1);
        outcome.Move.Row.Should().Be(7);
        outcome.Move.Col.Should().Be(7);
        outcome.Move.Stone.Should().Be(Stone.Black);
        room.Game!.CurrentTurn.Should().Be(Stone.White);
        room.Game.Moves.Should().HaveCount(1);
    }

    [Fact]
    public void Ply_Strictly_Increments()
    {
        var room = PlayingRoom(out var b, out var w);
        room.PlayMove(b, new Position(7, 7), Now.AddMinutes(2));
        room.PlayMove(w, new Position(6, 6), Now.AddMinutes(3));
        room.PlayMove(b, new Position(7, 8), Now.AddMinutes(4));

        room.Game!.Moves.Select(m => m.Ply).Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public void Non_Playing_State_Throws()
    {
        var room = Room.Create(RoomId.NewId(), "Room", UserId.NewId(), Now);
        var act = () => room.PlayMove(UserId.NewId(), new Position(0, 0), Now);
        act.Should().Throw<RoomNotInPlayException>();
    }

    [Fact]
    public void Non_Player_Throws()
    {
        var room = PlayingRoom(out _, out _);
        var act = () => room.PlayMove(UserId.NewId(), new Position(0, 0), Now);
        act.Should().Throw<NotAPlayerException>();
    }

    [Fact]
    public void Wrong_Turn_Throws()
    {
        var room = PlayingRoom(out _, out var white);
        // 轮到黑方,白方调 → NotYourTurn
        var act = () => room.PlayMove(white, new Position(0, 0), Now);
        act.Should().Throw<NotYourTurnException>();
    }

    [Fact]
    public void Board_Violation_Bubbles_InvalidMove_And_State_Intact()
    {
        var room = PlayingRoom(out var b, out var w);
        room.PlayMove(b, new Position(7, 7), Now.AddMinutes(2));
        // 白方在已有子的位置落子
        var act = () => room.PlayMove(w, new Position(7, 7), Now.AddMinutes(3));
        act.Should().Throw<InvalidMoveException>();
        room.Game!.Moves.Should().HaveCount(1); // 未追加
        room.Game.CurrentTurn.Should().Be(Stone.White); // 未翻转
    }

    [Fact]
    public void Five_In_A_Row_Ends_Game_Black_Wins()
    {
        var room = PlayingRoom(out var b, out var w);

        // 黑在 (7,3..7,7) 连五;白任意下
        room.PlayMove(b, new Position(7, 3), Now.AddSeconds(1));
        room.PlayMove(w, new Position(0, 0), Now.AddSeconds(2));
        room.PlayMove(b, new Position(7, 4), Now.AddSeconds(3));
        room.PlayMove(w, new Position(0, 1), Now.AddSeconds(4));
        room.PlayMove(b, new Position(7, 5), Now.AddSeconds(5));
        room.PlayMove(w, new Position(0, 2), Now.AddSeconds(6));
        room.PlayMove(b, new Position(7, 6), Now.AddSeconds(7));
        room.PlayMove(w, new Position(0, 3), Now.AddSeconds(8));

        var finalOutcome = room.PlayMove(b, new Position(7, 7), Now.AddSeconds(9));

        finalOutcome.Result.Should().Be(GameResult.BlackWin);
        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.WinnerUserId.Should().Be(b);
        room.Game.Result.Should().Be(GameResult.BlackWin);
        room.Game.EndedAt.Should().Be(Now.AddSeconds(9));
        room.Game.EndReason.Should().Be(GameEndReason.Connected5);
    }
}
