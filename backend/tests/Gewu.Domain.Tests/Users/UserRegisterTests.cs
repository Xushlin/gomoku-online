namespace Gewu.Domain.Tests.Users;

public class UserRegisterTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Register_Sets_Initial_State()
    {
        var id = UserId.NewId();
        var email = new Email("alice@example.com");
        var username = new Username("Alice");

        var user = User.Register(id, email, username, "hashed-password", FixedNow);

        user.Id.Should().Be(id);
        user.Email.Should().Be(email);
        user.Username.Should().Be(username);
        user.PasswordHash.Should().Be("hashed-password");
        // 战绩不在这里了 —— 见 UserGameStatsTests。注册也不建战绩行:
        // 一个新用户在每个棋种上都还没下过,而"没有行"正是那个意思。
        user.IsActive.Should().BeTrue();
        user.IsBot.Should().BeFalse();
        user.CreatedAt.Should().Be(FixedNow);
        user.RefreshTokens.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_With_Blank_Password_Hash_Throws(string? hash)
    {
        var act = () => User.Register(
            UserId.NewId(),
            new Email("alice@example.com"),
            new Username("Alice"),
            hash!,
            FixedNow);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*password hash*non-empty*");
    }
}
