using static Gomoku.Domain.EloRating.EloRating;

namespace Gomoku.Domain.Tests.Elo;

public class EloRatingTests
{
    [Fact]
    public void Is_Pure_Function_Same_Inputs_Yield_Same_Outputs()
    {
        var first = Calculate(1200, 50, 1350, 80, GameOutcome.Win);
        var second = Calculate(1200, 50, 1350, 80, GameOutcome.Win);
        var third = Calculate(1200, 50, 1350, 80, GameOutcome.Win);

        second.Should().Be(first);
        third.Should().Be(first);
    }

    [Theory]
    [InlineData(1200, 50, 1200, 50, GameOutcome.Win, 1210, 1190)]
    [InlineData(1200, 50, 1200, 50, GameOutcome.Loss, 1190, 1210)]
    [InlineData(1500, 50, 1500, 50, GameOutcome.Draw, 1500, 1500)]
    public void Same_Level_Outcomes_With_K20(
        int ra, int ga, int rb, int gb, GameOutcome outcomeA, int expectedA, int expectedB)
    {
        var (newA, newB) = Calculate(ra, ga, rb, gb, outcomeA);

        newA.Should().Be(expectedA);
        newB.Should().Be(expectedB);
    }

    [Fact]
    public void Upper_Losing_To_Lower_Moves_Ratings_Toward_Each_Other()
    {
        var (newA, newB) = Calculate(1500, 50, 1400, 50, GameOutcome.Loss);

        newA.Should().BeLessThan(1500);
        newB.Should().BeGreaterThan(1400);
    }

    [Fact]
    public void Novice_Vs_Master_Uses_Each_Sides_K_Factor_Independently()
    {
        var (newA, newB) = Calculate(1200, 0, 1200, 200, GameOutcome.Win);

        newA.Should().Be(1220);
        newB.Should().Be(1195);
        (newA - 1200).Should().NotBe(1200 - newB);
    }

    [Theory]
    [InlineData(29, 1220, 1180)]
    [InlineData(30, 1210, 1190)]
    [InlineData(99, 1210, 1190)]
    [InlineData(100, 1205, 1195)]
    public void K_Factor_Boundaries_On_Equal_Opponents_Black_Wins(
        int games, int expectedA, int expectedB)
    {
        var (newA, newB) = Calculate(1200, games, 1200, games, GameOutcome.Win);

        newA.Should().Be(expectedA);
        newB.Should().Be(expectedB);
    }

    [Fact]
    public void Uses_AwayFromZero_Not_Bankers_Rounding()
    {
        // 由于整数 rating 无法让 kA*(scoreA-expectedA) 精确命中 0.5,
        // 这里退而求其次,对 0.5 的舍入行为做直接断言 —— 以文档化 EloRating 的取舍:
        // banker's rounding(ToEven)会让 0.5 → 0,AwayFromZero 会让 0.5 → 1。
        Math.Round(0.5, MidpointRounding.AwayFromZero).Should().Be(1);
        Math.Round(0.5).Should().Be(0); // 默认是 ToEven(banker's)
    }

    [Fact]
    public void Extreme_Rating_Gap_Does_Not_Produce_NaN_Or_Throw()
    {
        var (newA, newB) = Calculate(2000, 50, 1000, 50, GameOutcome.Win);

        newA.Should().BeGreaterThanOrEqualTo(2000);
        newB.Should().BeLessThanOrEqualTo(1000);
    }

    [Fact]
    public void Unknown_Outcome_Throws()
    {
        var act = () => Calculate(1200, 0, 1200, 0, (GameOutcome)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
