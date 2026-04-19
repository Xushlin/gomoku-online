namespace Gomoku.Domain.Tests.Rooms;

public class RoomCreateTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Sets_Initial_State()
    {
        var id = RoomId.NewId();
        var host = UserId.NewId();
        var room = Room.Create(id, "  Alice's Room  ", host, Now);

        room.Id.Should().Be(id);
        room.Name.Should().Be("Alice's Room"); // trimmed
        room.HostUserId.Should().Be(host);
        room.BlackPlayerId.Should().Be(host);
        room.WhitePlayerId.Should().BeNull();
        room.Status.Should().Be(RoomStatus.Waiting);
        room.CreatedAt.Should().Be(Now);
        room.LastUrgeAt.Should().BeNull();
        room.LastUrgeByUserId.Should().BeNull();
        room.Game.Should().BeNull();
        room.Spectators.Should().BeEmpty();
        room.ChatMessages.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void Create_Blank_Name_Throws(string? name)
    {
        var act = () => Room.Create(RoomId.NewId(), name!, UserId.NewId(), Now);
        act.Should().Throw<Gomoku.Domain.Exceptions.InvalidRoomNameException>();
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a")]
    public void Create_Short_Name_Throws(string name)
    {
        var act = () => Room.Create(RoomId.NewId(), name, UserId.NewId(), Now);
        act.Should().Throw<Gomoku.Domain.Exceptions.InvalidRoomNameException>()
            .WithMessage("*out of range*");
    }

    [Fact]
    public void Create_Long_Name_Throws()
    {
        var long51 = new string('x', 51);
        var act = () => Room.Create(RoomId.NewId(), long51, UserId.NewId(), Now);
        act.Should().Throw<Gomoku.Domain.Exceptions.InvalidRoomNameException>()
            .WithMessage("*out of range*");
    }
}
