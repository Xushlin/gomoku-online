namespace Gomoku.Domain.Tests.Users;

public class UserIdTests
{
    [Fact]
    public void Wraps_Guid_Value()
    {
        var guid = Guid.NewGuid();
        var id = new UserId(guid);
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void Equal_When_Underlying_Guids_Are_Equal()
    {
        var guid = Guid.NewGuid();
        var a = new UserId(guid);
        var b = new UserId(guid);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void NewId_Produces_Distinct_Values()
    {
        var a = UserId.NewId();
        var b = UserId.NewId();
        a.Should().NotBe(b);
    }
}
