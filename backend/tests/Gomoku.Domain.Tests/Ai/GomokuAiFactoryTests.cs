namespace Gomoku.Domain.Tests.Ai;

public class GomokuAiFactoryTests
{
    [Fact]
    public void Easy_Branch_Returns_EasyAi()
    {
        var ai = GomokuAiFactory.Create(BotDifficulty.Easy, new Random(1));

        ai.Should().BeOfType<EasyAi>();
    }

    [Fact]
    public void Medium_Branch_Returns_MediumAi()
    {
        var ai = GomokuAiFactory.Create(BotDifficulty.Medium, new Random(1));

        ai.Should().BeOfType<MediumAi>();
    }

    [Fact]
    public void Hard_Branch_Returns_HardAi()
    {
        var ai = GomokuAiFactory.Create(BotDifficulty.Hard, new Random(1));

        ai.Should().BeOfType<HardAi>();
    }

    [Fact]
    public void Undefined_Difficulty_Throws()
    {
        var act = () => GomokuAiFactory.Create((BotDifficulty)99, new Random(1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Null_Random_Throws()
    {
        var act = () => GomokuAiFactory.Create(BotDifficulty.Easy, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
