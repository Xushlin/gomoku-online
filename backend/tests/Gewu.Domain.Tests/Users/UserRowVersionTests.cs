using Gewu.Domain.Games.Abstractions;

namespace Gewu.Domain.Tests.Users;

/// <summary>
/// <c>User.RowVersion</c> 现在只保护**改密码**;战绩写入推的是 <c>UserGameStats</c> 那一行自己的
/// 令牌(见 <c>UserGameStatsTests</c>)。分成两个的收益很具体:一个玩家一边下棋一边改密码,
/// 此前会撞 409。这里的用例盯着"哪条路推、哪条路不推"这条边界。
/// </summary>
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
    public void ChangePassword_Changes_RowVersion()
    {
        // 改密码是本聚合上**唯一**推进令牌的路径了。
        var u = NewUser();
        var before = (byte[])u.RowVersion.Clone();

        u.ChangePassword("new-hash");

        u.RowVersion.Should().NotEqual(before);
    }

    [Fact]
    public void Recording_A_Game_Does_Not_Touch_The_User_RowVersion()
    {
        // 这条是把令牌拆成两个之后新出现的边界:下棋写的是另一行,User 这行不该被推。
        // 它保证了"一边下棋一边改密码"不再互撞 409。
        var u = NewUser();
        var stats = UserGameStats.Start(u.Id, GameKeys.Gomoku);
        var before = (byte[])u.RowVersion.Clone();

        stats.RecordGameResult(GameOutcome.Win, 1220);

        u.RowVersion.Should().Equal(before);
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
