using Gomoku.Domain.Exceptions;
using Move = Gomoku.Domain.ValueObjects.Move;

namespace Gomoku.Domain.Tests.Rooms;

public class RoomDissolveTests
{
    private static readonly DateTime Now = new(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

    private static Room NewWaitingRoom(out UserId hostId)
    {
        hostId = UserId.NewId();
        return Room.Create(RoomId.NewId(), "Test Room", hostId, Now);
    }

    [Fact]
    public void Host_Can_Dissolve_Waiting_Room()
    {
        var room = NewWaitingRoom(out var hostId);

        var act = () => room.Dissolve(hostId);

        act.Should().NotThrow();
        // 校验:聚合状态未被修改
        room.Status.Should().Be(RoomStatus.Waiting);
        room.HostUserId.Should().Be(hostId);
        room.BlackPlayerId.Should().Be(hostId);
        room.WhitePlayerId.Should().BeNull();
    }

    [Fact]
    public void Non_Host_Cannot_Dissolve()
    {
        var room = NewWaitingRoom(out _);
        var stranger = UserId.NewId();

        var act = () => room.Dissolve(stranger);

        act.Should().Throw<NotRoomHostException>()
            .WithMessage($"*{stranger.Value}*is not the host*");
    }

    [Fact]
    public void Dissolve_Playing_Room_Throws_RoomNotWaiting()
    {
        var room = NewWaitingRoom(out var hostId);
        room.JoinAsPlayer(UserId.NewId(), Now.AddMinutes(1));
        room.Status.Should().Be(RoomStatus.Playing);

        var act = () => room.Dissolve(hostId);

        act.Should().Throw<RoomNotWaitingException>();
    }

    [Fact]
    public void Dissolve_Finished_Room_Throws_RoomNotWaiting()
    {
        var room = NewWaitingRoom(out var hostId);
        var whiteId = UserId.NewId();
        room.JoinAsPlayer(whiteId, Now.AddMinutes(1));

        // 黑方横向连五结束对局
        for (var c = 0; c <= 3; c++)
        {
            room.PlayMove(hostId, new Position(7, c), Now.AddMinutes(2).AddSeconds(c * 2));
            room.PlayMove(whiteId, new Position(8, c), Now.AddMinutes(2).AddSeconds(c * 2 + 1));
        }
        room.PlayMove(hostId, new Position(7, 4), Now.AddMinutes(3));
        room.Status.Should().Be(RoomStatus.Finished);

        var act = () => room.Dissolve(hostId);

        act.Should().Throw<RoomNotWaitingException>();
    }

    [Fact]
    public void Host_Can_Dissolve_Waiting_Room_With_Spectators_And_Chat()
    {
        var room = NewWaitingRoom(out var hostId);
        var spec1 = UserId.NewId();
        var spec2 = UserId.NewId();
        room.JoinAsSpectator(spec1);
        room.JoinAsSpectator(spec2);
        room.PostChatMessage(spec1, "S1", "hi", ChatChannel.Spectator, Now.AddMinutes(1));

        var act = () => room.Dissolve(hostId);

        act.Should().NotThrow();
        // 物理删除由仓储负责,聚合本身不丢围观者 / 聊天
        room.Spectators.Should().HaveCount(2);
        room.ChatMessages.Should().HaveCount(1);
    }

    [Fact]
    public void Spectator_Trying_To_Dissolve_Throws_NotRoomHost()
    {
        var room = NewWaitingRoom(out _);
        var spec = UserId.NewId();
        room.JoinAsSpectator(spec);

        var act = () => room.Dissolve(spec);

        act.Should().Throw<NotRoomHostException>();
    }
}
