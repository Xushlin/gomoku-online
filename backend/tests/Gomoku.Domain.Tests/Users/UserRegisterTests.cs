namespace Gomoku.Domain.Tests.Users;

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
        user.Rating.Should().Be(1200);
        user.GamesPlayed.Should().Be(0);
        user.Wins.Should().Be(0);
        user.Losses.Should().Be(0);
        user.Draws.Should().Be(0);
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
