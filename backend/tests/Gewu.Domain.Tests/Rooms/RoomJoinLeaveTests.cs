using Gomoku.Domain.Exceptions;
using Gomoku.Domain.Enums;

namespace Gomoku.Domain.Tests.Rooms;

public class RoomJoinLeaveTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private static Room NewRoom(out UserId hostId)
    {
        hostId = UserId.NewId();
        return Room.Create(RoomId.NewId(), "Test Room", hostId, Now);
    }

    [Fact]
    public void Second_Player_Joins_And_Starts_Game()
    {
        var room = NewRoom(out _);
        var bobId = UserId.NewId();

        room.JoinAsPlayer(bobId, Now.AddMinutes(1));

        room.WhitePlayerId.Should().Be(bobId);
        room.Status.Should().Be(RoomStatus.Playing);
        room.Game.Should().NotBeNull();
        room.Game!.CurrentTurn.Should().Be(Stone.Black);
        room.Game.StartedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Host_Rejoin_Throws()
    {
        var room = NewRoom(out var hostId);
        var act = () => room.JoinAsPlayer(hostId, Now.AddMinutes(1));
        act.Should().Throw<AlreadyInRoomException>();
    }

    [Fact]
    public void Room_Full_Throws()
    {
        var room = NewRoom(out _);
        room.JoinAsPlayer(UserId.NewId(), Now.AddMinutes(1));
        var act = () => room.JoinAsPlayer(UserId.NewId(), Now.AddMinutes(2));
        act.Should().Throw<RoomNotWaitingException>();
    }

    [Fact]
    public void Spectator_Upgraded_To_Player_On_Join()
    {
        var room = NewRoom(out _);
        var bobId = UserId.NewId();
        room.JoinAsSpectator(bobId);
        room.Spectators.Should().Contain(bobId);

        room.JoinAsPlayer(bobId, Now.AddMinutes(1));

        room.WhitePlayerId.Should().Be(bobId);
        room.Spectators.Should().NotContain(bobId);
    }

    [Fact]
    public void Spectator_Leave_Removes_From_Set()
    {
        var room = NewRoom(out _);
        var carol = UserId.NewId();
        room.JoinAsSpectator(carol);

        room.LeaveAsSpectator(carol);

        room.Spectators.Should().NotContain(carol);
    }

    [Fact]
    public void Leave_Spectator_Not_Spectating_Throws()
    {
        var room = NewRoom(out _);
        var act = () => room.LeaveAsSpectator(UserId.NewId());
        act.Should().Throw<NotSpectatingException>();
    }

    [Fact]
    public void Player_Cannot_Spectate()
    {
        var room = NewRoom(out var hostId);
        var act = () => room.JoinAsSpectator(hostId);
        act.Should().Throw<PlayerCannotSpectateException>();
    }

    [Fact]
    public void Spectator_Join_Idempotent()
    {
        var room = NewRoom(out _);
        var c = UserId.NewId();
        room.JoinAsSpectator(c);
        room.JoinAsSpectator(c);
        room.Spectators.Count(x => x == c).Should().Be(1);
    }

    [Fact]
    public void Host_Cannot_Leave_Waiting_Room()
    {
        var room = NewRoom(out var hostId);
        var act = () => room.Leave(hostId, Now.AddMinutes(1));
        act.Should().Throw<HostCannotLeaveWaitingRoomException>();
    }

    [Fact]
    public void Non_Member_Leave_Throws()
    {
        var room = NewRoom(out _);
        var act = () => room.Leave(UserId.NewId(), Now.AddMinutes(1));
        act.Should().Throw<NotInRoomException>();
    }

    [Fact]
    public void Player_Leave_During_Play_Does_Not_Change_Game()
    {
        var room = NewRoom(out var host);
        var bob = UserId.NewId();
        room.JoinAsPlayer(bob, Now.AddMinutes(1));
        var gameBefore = room.Game;

        room.Leave(host, Now.AddMinutes(2));

        room.Game.Should().Be(gameBefore);
        room.Status.Should().Be(RoomStatus.Playing); // 不自动判负
    }
}
