using Gomoku.Domain.Enums;

namespace Gomoku.Domain.Tests.Rooms;

public class RoomSwapPlayersTests
{
    private static readonly DateTime Now = new(2026, 4, 30, 12, 0, 0, DateTimeKind.Utc);

    private static Room PlayingRoom(out UserId hostId, out UserId opponentId)
    {
        hostId = UserId.NewId();
        opponentId = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "Swap Test", hostId, Now);
        room.JoinAsPlayer(opponentId, Now.AddSeconds(1));
        return room;
    }

    [Fact]
    public void Swap_Exchanges_Black_And_White_Ids()
    {
        var room = PlayingRoom(out var hostId, out var opponentId);

        room.SwapPlayers(Now.AddSeconds(2));

        room.BlackPlayerId.Should().Be(opponentId);
        room.WhitePlayerId.Should().Be(hostId);
    }

    [Fact]
    public void Swap_Does_Not_Touch_Host_Or_CurrentTurn()
    {
        var room = PlayingRoom(out var hostId, out _);
        var initialTurn = room.Game!.CurrentTurn;

        room.SwapPlayers(Now.AddSeconds(2));

        room.HostUserId.Should().Be(hostId);
        room.Game!.CurrentTurn.Should().Be(initialTurn);
        room.Game.CurrentTurn.Should().Be(Stone.Black);
    }

    [Fact]
    public void Swap_After_First_Move_Throws()
    {
        var room = PlayingRoom(out _, out _);
        // Make any legal move (host = black, plays first).
        room.PlayMove(room.BlackPlayerId, new Position(7, 7), Now.AddSeconds(2));

        var act = () => room.SwapPlayers(Now.AddSeconds(3));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*after the first move*");
    }

    [Fact]
    public void Swap_While_Waiting_Throws()
    {
        // Status = Waiting (no opponent yet).
        var hostId = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "Waiting Room", hostId, Now);

        var act = () => room.SwapPlayers(Now.AddSeconds(1));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Waiting*");
    }

    [Fact]
    public void Swap_After_Resign_Throws()
    {
        var room = PlayingRoom(out var hostId, out _);
        // Host resigns immediately → Status = Finished.
        room.Resign(hostId, Now.AddSeconds(2));
        room.Status.Should().Be(RoomStatus.Finished);

        var act = () => room.SwapPlayers(Now.AddSeconds(3));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Finished*");
    }
}
