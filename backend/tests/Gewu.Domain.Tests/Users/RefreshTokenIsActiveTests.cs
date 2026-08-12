namespace Gomoku.Domain.Tests.Users;

public class RefreshTokenIsActiveTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private static User NewUser() =>
        User.Register(
            UserId.NewId(),
            new Email("alice@example.com"),
            new Username("Alice"),
            "hashed",
            Now);

    [Fact]
    public void Active_When_Not_Revoked_And_Not_Expired()
    {
        var user = NewUser();
        user.IssueRefreshToken("h1", Now.AddDays(7), Now);
        var token = user.RefreshTokens.Single();

        token.IsActive(Now.AddHours(1)).Should().BeTrue();
    }

    [Fact]
    public void Inactive_When_Revoked()
    {
        var user = NewUser();
        user.IssueRefreshToken("h1", Now.AddDays(7), Now);
        user.RevokeRefreshToken("h1", Now.AddHours(1));
        var token = user.RefreshTokens.Single();

        token.IsActive(Now.AddHours(2)).Should().BeFalse();
    }

    [Fact]
    public void Inactive_When_Expired()
    {
        var user = NewUser();
        user.IssueRefreshToken("h1", Now.AddDays(7), Now);
        var token = user.RefreshTokens.Single();

        token.IsActive(Now.AddDays(8)).Should().BeFalse();
    }

    [Fact]
    public void Inactive_When_Exactly_At_Expiration()
    {
        var user = NewUser();
        var expiresAt = Now.AddDays(7);
        user.IssueRefreshToken("h1", expiresAt, Now);
        var token = user.RefreshTokens.Single();

        token.IsActive(expiresAt).Should().BeFalse();
    }
}
