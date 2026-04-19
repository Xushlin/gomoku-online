namespace Gomoku.Domain.Tests.Users;

public class UserRegisterBotTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RegisterBot_Sets_Initial_State()
    {
        var id = UserId.NewId();
        var email = new Email("easy@bot.gomoku.local");
        var username = new Username("AI_Easy");

        var bot = User.RegisterBot(id, email, username, FixedNow);

        bot.Id.Should().Be(id);
        bot.Email.Should().Be(email);
        bot.Username.Should().Be(username);
        bot.PasswordHash.Should().Be(User.BotPasswordHashMarker);
        bot.Rating.Should().Be(1200);
        bot.GamesPlayed.Should().Be(0);
        bot.Wins.Should().Be(0);
        bot.Losses.Should().Be(0);
        bot.Draws.Should().Be(0);
        bot.IsActive.Should().BeTrue();
        bot.IsBot.Should().BeTrue();
        bot.CreatedAt.Should().Be(FixedNow);
        bot.RefreshTokens.Should().BeEmpty();
    }

    [Fact]
    public void BotPasswordHashMarker_Is_Stable_Sentinel()
    {
        User.BotPasswordHashMarker.Should().Be("__BOT_NO_LOGIN__");
    }

    [Fact]
    public void RegisterBot_Uses_Marker_Not_Any_Real_Hash_Format()
    {
        // V3 PasswordHasher 输出总以 ASCII base64 形式出现(含大小写 + 数字 + '/' + '+'
        // + '=' padding),不会出现下划线;marker 形如 "__BOT_NO_LOGIN__",下划线足以和
        // 任何合法 hash 区分,确保不会误把真人 hash 当作 bot 标记,也不会把 bot 当成能
        // 走 PasswordHasher.Verify 的对象。
        User.BotPasswordHashMarker.Should().Contain("_");
        User.BotPasswordHashMarker.Should().NotContain("=");
    }

    [Fact]
    public void Two_RegisterBot_Calls_Produce_Independent_Instances()
    {
        var idA = UserId.NewId();
        var idB = UserId.NewId();
        var email = new Email("m@bot.gomoku.local");
        var username = new Username("AI_Medium");

        var a = User.RegisterBot(idA, email, username, FixedNow);
        var b = User.RegisterBot(idB, email, username, FixedNow.AddDays(1));

        a.Id.Should().NotBe(b.Id);
        a.CreatedAt.Should().NotBe(b.CreatedAt);
        a.IsBot.Should().BeTrue();
        b.IsBot.Should().BeTrue();
    }
}
