namespace Gomoku.Domain.Tests.Users;

public class UserRowVersionTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 21, 12, 0, 0, DateTimeKind.Utc);

    private static User NewUser(string username = "Alice", string email = "alice@example.com") =>
        User.Register(
            UserId.NewId(),
            new Email(email),
            new Username(username),
            "HASHED",
            FixedNow);

    [Fact]
    public void Register_User_Has_Nonempty_RowVersion_16Bytes()
    {
        var u = NewUser();

        u.RowVersion.Should().NotBeNull();
        u.RowVersion.Length.Should().Be(16);
    }

    [Fact]
    public void RegisterBot_Has_Nonempty_RowVersion_16Bytes()
    {
        var bot = User.RegisterBot(
            UserId.NewId(),
            new Email("easy@bot.gomoku.local"),
            new Username("AI_Easy"),
            FixedNow);

        bot.RowVersion.Should().NotBeNull();
        bot.RowVersion.Length.Should().Be(16);
    }

    [Fact]
    public void Two_Registered_Users_Have_Different_RowVersions()
    {
        var a = NewUser("Alice", "alice@example.com");
        var b = NewUser("Bob", "bob@example.com");

        a.RowVersion.Should().NotEqual(b.RowVersion);
    }

    [Fact]
    public void RecordGameResult_Changes_RowVersion()
    {
        var u = NewUser();
        var before = (byte[])u.RowVersion.Clone();

        u.RecordGameResult(GameOutcome.Win, 1220);

        u.RowVersion.Should().NotEqual(before);
    }

    [Fact]
    public void Three_Successive_RecordGameResult_Yield_Three_Distinct_RowVersions()
    {
        var u = NewUser();
        var versions = new List<byte[]>();

        u.RecordGameResult(GameOutcome.Win, 1220);
        versions.Add((byte[])u.RowVersion.Clone());

        u.RecordGameResult(GameOutcome.Loss, 1204);
        versions.Add((byte[])u.RowVersion.Clone());

        u.RecordGameResult(GameOutcome.Draw, 1204);
        versions.Add((byte[])u.RowVersion.Clone());

        versions[0].Should().NotEqual(versions[1]);
        versions[1].Should().NotEqual(versions[2]);
        versions[0].Should().NotEqual(versions[2]);
    }

    [Fact]
    public void Invalid_Outcome_Does_Not_Change_RowVersion()
    {
        var u = NewUser();
        var before = (byte[])u.RowVersion.Clone();

        var act = () => u.RecordGameResult((GameOutcome)99, 1200);
        act.Should().Throw<ArgumentOutOfRangeException>();

        u.RowVersion.Should().Equal(before);
        u.GamesPlayed.Should().Be(0);
        u.Rating.Should().Be(1200);
    }

    [Fact]
    public void IssueRefreshToken_Does_Not_Change_RowVersion()
    {
        var u = NewUser();
        var before = (byte[])u.RowVersion.Clone();

        u.IssueRefreshToken("hash1", FixedNow.AddDays(7), FixedNow);

        u.RowVersion.Should().Equal(before);
    }

    [Fact]
    public void RevokeRefreshToken_Does_Not_Change_RowVersion()
    {
        var u = NewUser();
        u.IssueRefreshToken("hash1", FixedNow.AddDays(7), FixedNow);
        var before = (byte[])u.RowVersion.Clone();

        u.RevokeRefreshToken("hash1", FixedNow.AddHours(1));

        u.RowVersion.Should().Equal(before);
    }

    [Fact]
    public void RevokeAllRefreshTokens_Does_Not_Change_RowVersion()
    {
        var u = NewUser();
        u.IssueRefreshToken("hash1", FixedNow.AddDays(7), FixedNow);
        u.IssueRefreshToken("hash2", FixedNow.AddDays(7), FixedNow);
        var before = (byte[])u.RowVersion.Clone();

        u.RevokeAllRefreshTokens(FixedNow.AddHours(1));

        u.RowVersion.Should().Equal(before);
    }
}
