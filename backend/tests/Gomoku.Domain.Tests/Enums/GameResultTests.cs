namespace Gomoku.Domain.Tests.Enums;

public class GameResultTests
{
    [Theory]
    [InlineData(GameResult.Ongoing)]
    [InlineData(GameResult.BlackWin)]
    [InlineData(GameResult.WhiteWin)]
    [InlineData(GameResult.Draw)]
    public void Enum_Exposes_Four_States(GameResult value)
    {
        Enum.IsDefined(typeof(GameResult), value).Should().BeTrue();
    }

    [Fact]
    public void Default_Is_Ongoing()
    {
        GameResult value = default;
        value.Should().Be(GameResult.Ongoing);
    }
}
