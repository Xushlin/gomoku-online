namespace Gomoku.Domain.Tests.Rooms;

public class RoomIdTests
{
    [Fact]
    public void Wraps_Guid()
    {
        var g = Guid.NewGuid();
        var id = new RoomId(g);
        id.Value.Should().Be(g);
    }

    [Fact]
    public void Value_Equality()
    {
        var g = Guid.NewGuid();
        (new RoomId(g)).Should().Be(new RoomId(g));
    }

    [Fact]
    public void NewId_Unique()
    {
        RoomId.NewId().Should().NotBe(RoomId.NewId());
    }
}
