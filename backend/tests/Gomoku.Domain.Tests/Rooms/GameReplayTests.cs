using Gomoku.Domain.Enums;
using Gomoku.Domain.ValueObjects;

namespace Gomoku.Domain.Tests.Rooms;

public class GameReplayTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private static Room PlayingRoom(out UserId black, out UserId white)
    {
        black = UserId.NewId();
        white = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "Replay", black, Now);
        room.JoinAsPlayer(white, Now.AddMinutes(1));
        return room;
    }

    [Fact]
    public void Empty_Moves_Yields_Empty_Board()
    {
        var room = PlayingRoom(out _, out _);
        var board = room.Game!.ReplayBoard();

        board.GetStone(new Position(0, 0)).Should().Be(Stone.Empty);
        board.GetStone(new Position(7, 7)).Should().Be(Stone.Empty);
        board.GetStone(new Position(14, 14)).Should().Be(Stone.Empty);
    }

    [Fact]
    public void Replay_Reflects_All_Moves()
    {
        var room = PlayingRoom(out var b, out var w);
        room.PlayMove(b, new Position(7, 7), Now.AddSeconds(1));
        room.PlayMove(w, new Position(8, 8), Now.AddSeconds(2));
        room.PlayMove(b, new Position(7, 8), Now.AddSeconds(3));

        var board = room.Game!.ReplayBoard();

        board.GetStone(new Position(7, 7)).Should().Be(Stone.Black);
        board.GetStone(new Position(8, 8)).Should().Be(Stone.White);
        board.GetStone(new Position(7, 8)).Should().Be(Stone.Black);
        board.GetStone(new Position(7, 9)).Should().Be(Stone.Empty);
    }
}
