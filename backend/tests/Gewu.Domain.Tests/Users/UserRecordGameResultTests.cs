namespace Gomoku.Domain.Tests.Users;

public class UserRecordGameResultTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private static User NewUser() =>
        User.Register(UserId.NewId(), new Email("alice@example.com"), new Username("Alice"), "HASHED", FixedNow);

    [Fact]
    public void Win_Increments_GamesPlayed_And_Wins_And_Sets_Rating()
    {
        var user = NewUser();

        user.RecordGameResult(GameOutcome.Win, 1216);

        user.GamesPlayed.Should().Be(1);
        user.Wins.Should().Be(1);
        user.Losses.Should().Be(0);
        user.Draws.Should().Be(0);
        user.Rating.Should().Be(1216);
    }

    [Fact]
    public void Loss_Increments_GamesPlayed_And_Losses_And_Sets_Rating()
    {
        var user = NewUser();

        user.RecordGameResult(GameOutcome.Loss, 1184);

        user.GamesPlayed.Should().Be(1);
        user.Wins.Should().Be(0);
        user.Losses.Should().Be(1);
        user.Draws.Should().Be(0);
        user.Rating.Should().Be(1184);
    }

    [Fact]
    public void Draw_Increments_GamesPlayed_And_Draws_And_Sets_Rating()
    {
        var user = NewUser();

        user.RecordGameResult(GameOutcome.Draw, 1200);

        user.GamesPlayed.Should().Be(1);
        user.Wins.Should().Be(0);
        user.Losses.Should().Be(0);
        user.Draws.Should().Be(1);
        user.Rating.Should().Be(1200);
    }

    [Fact]
    public void Multiple_Results_Keep_Counters_In_Sync_With_GamesPlayed()
    {
        var user = NewUser();

        user.RecordGameResult(GameOutcome.Win, 1216);
        user.RecordGameResult(GameOutcome.Loss, 1200);
        user.RecordGameResult(GameOutcome.Draw, 1200);

        user.GamesPlayed.Should().Be(3);
        user.Wins.Should().Be(1);
        user.Losses.Should().Be(1);
        user.Draws.Should().Be(1);
        user.Rating.Should().Be(1200);
        (user.Wins + user.Losses + user.Draws).Should().Be(user.GamesPlayed);
    }

    [Fact]
    public void Unknown_Outcome_Throws_And_Preserves_State()
    {
        var user = NewUser();

        var act = () => user.RecordGameResult((GameOutcome)99, 9999);

        act.Should().Throw<ArgumentOutOfRangeException>();
        user.GamesPlayed.Should().Be(0);
        user.Wins.Should().Be(0);
        user.Losses.Should().Be(0);
        user.Draws.Should().Be(0);
        user.Rating.Should().Be(1200);
    }

    [Fact]
    public void Invariant_Holds_After_Many_Mixed_Results()
    {
        var user = NewUser();
        var sequence = new[]
        {
            GameOutcome.Win, GameOutcome.Win, GameOutcome.Loss, GameOutcome.Draw,
            GameOutcome.Win, GameOutcome.Loss, GameOutcome.Draw, GameOutcome.Loss,
        };

        foreach (var outcome in sequence)
        {
            user.RecordGameResult(outcome, user.Rating);
        }

        user.GamesPlayed.Should().Be(sequence.Length);
        (user.Wins + user.Losses + user.Draws).Should().Be(user.GamesPlayed);
    }
}
