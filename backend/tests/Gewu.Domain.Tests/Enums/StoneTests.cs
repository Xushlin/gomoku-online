namespace Gomoku.Domain.Tests.Enums;

public class StoneTests
{
    [Fact]
    public void Default_Is_Empty()
    {
        Stone value = default;
        value.Should().Be(Stone.Empty);
    }

    [Fact]
    public void Empty_Has_Underlying_Value_Zero()
    {
        ((int)Stone.Empty).Should().Be(0);
    }
}
