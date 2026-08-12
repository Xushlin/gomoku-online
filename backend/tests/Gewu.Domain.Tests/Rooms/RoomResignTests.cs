using Gomoku.Domain.Exceptions;
using Move = Gomoku.Domain.ValueObjects.Move;

namespace Gomoku.Domain.Tests.Rooms;

public class RoomResignTests
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
    public void Black_Resigns_White_Wins()
    {
        var (room, black, white) = PlayingRoom();

        var outcome = room.Resign(black, Now.AddMinutes(1));

        outcome.Result.Should().Be(GameResult.WhiteWin);
        outcome.WinnerUserId.Should().Be(white);
        room.Game!.Result.Should().Be(GameResult.WhiteWin);
        room.Game.WinnerUserId.Should().Be(white);
        room.Game.EndReason.Should().Be(GameEndReason.Resigned);
        room.Game.EndedAt.Should().Be(Now.AddMinutes(1));
        room.Status.Should().Be(RoomStatus.Finished);
    }

    [Fact]
    public void White_Resigns_Black_Wins()
    {
        var (room, black, white) = PlayingRoom();

        var outcome = room.Resign(white, Now.AddMinutes(1));

        outcome.Result.Should().Be(GameResult.BlackWin);
        outcome.WinnerUserId.Should().Be(black);
        room.Game!.EndReason.Should().Be(GameEndReason.Resigned);
        room.Status.Should().Be(RoomStatus.Finished);
    }

    [Fact]
    public void Resign_On_Opponents_Turn_Is_Allowed()
    {
        var (room, black, white) = PlayingRoom();
        // Black 走一手,现在轮到 White
        room.PlayMove(black, new Position(7, 7), Now.AddSeconds(2));
        room.Game!.CurrentTurn.Should().Be(Stone.White);

        // Black 在 White 回合认输也成功
        var outcome = room.Resign(black, Now.AddMinutes(1));

        outcome.Result.Should().Be(GameResult.WhiteWin);
        room.Status.Should().Be(RoomStatus.Finished);
    }

    [Fact]
    public void Resign_In_Waiting_Room_Throws()
    {
        var host = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "waiting", host, Now);

        var act = () => room.Resign(host, Now);

        act.Should().Throw<RoomNotInPlayException>();
    }

    [Fact]
    public void Resign_In_Finished_Room_Throws()
    {
        var (room, black, _) = PlayingRoom();
        room.Resign(black, Now.AddMinutes(1)); // 进入 Finished

        var act = () => room.Resign(black, Now.AddMinutes(2));

        act.Should().Throw<RoomNotInPlayException>();
    }

    [Fact]
    public void Non_Player_Resign_Throws()
    {
        var (room, _, _) = PlayingRoom();
        var stranger = UserId.NewId();

        var act = () => room.Resign(stranger, Now.AddMinutes(1));

        act.Should().Throw<NotAPlayerException>();
    }
}
