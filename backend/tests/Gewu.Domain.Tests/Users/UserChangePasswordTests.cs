namespace Gomoku.Domain.Tests.Users;

public class UserChangePasswordTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 21, 12, 0, 0, DateTimeKind.Utc);

    private static User NewUser() => User.Register(
        UserId.NewId(),
        new Email("alice@example.com"),
        new Username("Alice"),
        "oldhash",
        FixedNow);

    [Fact]
    public void ChangePassword_Replaces_Hash_And_Touches_RowVersion()
    {
        var u = NewUser();
        var beforeHash = u.PasswordHash;
        var beforeVersion = (byte[])u.RowVersion.Clone();

        u.ChangePassword("newhash");

        u.PasswordHash.Should().Be("newhash");
        u.PasswordHash.Should().NotBe(beforeHash);
        u.RowVersion.Should().NotEqual(beforeVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChangePassword_With_Blank_Hash_Throws(string? blank)
    {
        var u = NewUser();
        var beforeHash = u.PasswordHash;
        var beforeVersion = (byte[])u.RowVersion.Clone();

        var act = () => u.ChangePassword(blank!);

        act.Should().Throw<ArgumentException>();
        u.PasswordHash.Should().Be(beforeHash);
        u.RowVersion.Should().Equal(beforeVersion);
    }

    [Fact]
    public void ChangePassword_On_Bot_Throws_InvalidOperation()
    {
        var bot = User.RegisterBot(
            UserId.NewId(),
            new Email("easy@bot.gomoku.local"),
            new Username("AI_Easy"),
            FixedNow);
        var beforeHash = bot.PasswordHash;
        var beforeVersion = (byte[])bot.RowVersion.Clone();

        var act = () => bot.ChangePassword("newhash");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Bot accounts cannot change password*");
        bot.PasswordHash.Should().Be(beforeHash);
        bot.RowVersion.Should().Equal(beforeVersion);
    }

    [Fact]
    public void Three_Successive_ChangePassword_Produce_Three_Distinct_RowVersions()
    {
        var u = NewUser();
        var versions = new List<byte[]>();

        versions.Add((byte[])u.RowVersion.Clone());
        u.ChangePassword("h1");
        versions.Add((byte[])u.RowVersion.Clone());
        u.ChangePassword("h2");
        versions.Add((byte[])u.RowVersion.Clone());
        u.ChangePassword("h3");
        versions.Add((byte[])u.RowVersion.Clone());

        // 4 个 RowVersion(初始 + 3 次改密)两两不等
        for (var i = 0; i < versions.Count; i++)
        {
            for (var j = i + 1; j < versions.Count; j++)
            {
                versions[i].Should().NotEqual(versions[j], $"version[{i}] vs version[{j}]");
            }
        }
    }
}
