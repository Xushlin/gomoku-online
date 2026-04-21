using Gomoku.Domain.Exceptions;
using Move = Gomoku.Domain.ValueObjects.Move;

namespace Gomoku.Domain.Tests.Rooms;

public class RoomTimeOutTests
{
    private static readonly DateTime Now = new(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);

    private static (Room Room, UserId Black, UserId White) PlayingRoom()
    {
        var host = UserId.NewId();
        var white = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "test", host, Now);
        room.JoinAsPlayer(white, Now.AddSeconds(1));
        return (room, host, white);
    }

    [Fact]
    public void Black_Times_Out_On_First_Move_White_Wins()
    {
        // Game.StartedAt = Now + 1s;CurrentTurn = Black;无 Moves
        var (room, _, white) = PlayingRoom();
        var later = Now.AddSeconds(1).AddSeconds(61); // 距 StartedAt 61s

        var outcome = room.TimeOutCurrentTurn(later, turnTimeoutSeconds: 60);

        outcome.Result.Should().Be(GameResult.WhiteWin);
        outcome.WinnerUserId.Should().Be(white);
        room.Game!.EndReason.Should().Be(GameEndReason.TurnTimeout);
        room.Game.WinnerUserId.Should().Be(white);
        room.Status.Should().Be(RoomStatus.Finished);
    }

    [Fact]
    public void White_Times_Out_After_Blacks_Move_Black_Wins()
    {
        var (room, black, _) = PlayingRoom();
        var blackMoveAt = Now.AddSeconds(2);
        room.PlayMove(black, new Position(7, 7), blackMoveAt);
        // CurrentTurn 现在是 White,lastActivity = blackMoveAt

        var later = blackMoveAt.AddSeconds(61);

        var outcome = room.TimeOutCurrentTurn(later, turnTimeoutSeconds: 60);

        outcome.Result.Should().Be(GameResult.BlackWin);
        outcome.WinnerUserId.Should().Be(black);
        room.Game!.EndReason.Should().Be(GameEndReason.TurnTimeout);
    }

    [Fact]
    public void Not_Yet_Timed_Out_Throws()
    {
        var (room, _, _) = PlayingRoom();
        var later = Now.AddSeconds(1).AddSeconds(59); // 59s elapsed, threshold 60s

        var act = () => room.TimeOutCurrentTurn(later, turnTimeoutSeconds: 60);

        act.Should().Throw<TurnNotTimedOutException>();
        room.Status.Should().Be(RoomStatus.Playing);
        room.Game!.Result.Should().BeNull();
        room.Game.EndReason.Should().BeNull();
    }

    [Fact]
    public void Exactly_On_Threshold_Succeeds()
    {
        // (now - lastActivity).TotalSeconds == turnTimeoutSeconds → >= 成功
        var (room, _, _) = PlayingRoom();
        var startedAt = Now.AddSeconds(1);
        var later = startedAt.AddSeconds(60); // exactly 60s

        var act = () => room.TimeOutCurrentTurn(later, turnTimeoutSeconds: 60);

        act.Should().NotThrow();
        room.Status.Should().Be(RoomStatus.Finished);
    }

    [Fact]
    public void TimeOut_In_Waiting_Room_Throws()
    {
        var host = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "waiting", host, Now);

        var act = () => room.TimeOutCurrentTurn(Now.AddMinutes(10), turnTimeoutSeconds: 60);

        act.Should().Throw<RoomNotInPlayException>();
    }

    [Fact]
    public void TimeOut_In_Finished_Room_Throws()
    {
        var (room, black, _) = PlayingRoom();
        room.Resign(black, Now.AddSeconds(2));

        var act = () => room.TimeOutCurrentTurn(Now.AddMinutes(10), turnTimeoutSeconds: 60);

        act.Should().Throw<RoomNotInPlayException>();
    }

    [Fact]
    public void Zero_Timeout_Argument_Throws()
    {
        var (room, _, _) = PlayingRoom();

        var act = () => room.TimeOutCurrentTurn(Now.AddMinutes(1), turnTimeoutSeconds: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
