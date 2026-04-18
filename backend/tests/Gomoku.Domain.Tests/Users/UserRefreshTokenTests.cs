namespace Gomoku.Domain.Tests.Users;

public class UserRefreshTokenTests
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
    public void IssueRefreshToken_Adds_Entry()
    {
        var user = NewUser();
        user.IssueRefreshToken("hash1", Now.AddDays(7), Now);

        user.RefreshTokens.Should().HaveCount(1);
        var token = user.RefreshTokens.Single();
        token.TokenHash.Should().Be("hash1");
        token.ExpiresAt.Should().Be(Now.AddDays(7));
        token.CreatedAt.Should().Be(Now);
        token.RevokedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IssueRefreshToken_Blank_Hash_Throws(string? hash)
    {
        var user = NewUser();
        var act = () => user.IssueRefreshToken(hash!, Now.AddDays(7), Now);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*token hash*non-empty*");
    }

    [Fact]
    public void IssueRefreshToken_NonPositive_Ttl_Throws()
    {
        var user = NewUser();
        var act = () => user.IssueRefreshToken("h", Now, Now);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*expiresAt*greater than*");
    }

    [Fact]
    public void RevokeRefreshToken_Found_Returns_True_And_Sets_Timestamp()
    {
        var user = NewUser();
        user.IssueRefreshToken("hash1", Now.AddDays(7), Now);

        var revokeAt = Now.AddHours(1);
        var result = user.RevokeRefreshToken("hash1", revokeAt);

        result.Should().BeTrue();
        user.RefreshTokens.Single().RevokedAt.Should().Be(revokeAt);
    }

    [Fact]
    public void RevokeRefreshToken_Not_Found_Returns_False()
    {
        var user = NewUser();
        user.IssueRefreshToken("hash1", Now.AddDays(7), Now);

        var result = user.RevokeRefreshToken("unknown-hash", Now.AddHours(1));

        result.Should().BeFalse();
        user.RefreshTokens.Single().RevokedAt.Should().BeNull();
    }

    [Fact]
    public void RevokeRefreshToken_Already_Revoked_Keeps_Original_Timestamp()
    {
        var user = NewUser();
        user.IssueRefreshToken("hash1", Now.AddDays(7), Now);

        var firstRevoke = Now.AddHours(1);
        var secondRevoke = Now.AddHours(2);
        user.RevokeRefreshToken("hash1", firstRevoke);
        user.RevokeRefreshToken("hash1", secondRevoke);

        user.RefreshTokens.Single().RevokedAt.Should().Be(firstRevoke);
    }

    [Fact]
    public void RevokeRefreshToken_Only_Affects_Matching_Token()
    {
        var user = NewUser();
        user.IssueRefreshToken("h1", Now.AddDays(7), Now);
        user.IssueRefreshToken("h2", Now.AddDays(7), Now);

        user.RevokeRefreshToken("h1", Now.AddHours(1));

        user.RefreshTokens.Single(t => t.TokenHash == "h1").RevokedAt.Should().NotBeNull();
        user.RefreshTokens.Single(t => t.TokenHash == "h2").RevokedAt.Should().BeNull();
    }

    [Fact]
    public void RevokeAllRefreshTokens_Revokes_Active_Only()
    {
        var user = NewUser();
        user.IssueRefreshToken("h1", Now.AddDays(7), Now);
        user.IssueRefreshToken("h2", Now.AddDays(7), Now);
        user.IssueRefreshToken("h3", Now.AddDays(7), Now);

        var firstRevokeTime = Now.AddHours(1);
        user.RevokeRefreshToken("h1", firstRevokeTime);

        var bulkRevokeTime = Now.AddHours(2);
        user.RevokeAllRefreshTokens(bulkRevokeTime);

        user.RefreshTokens.Single(t => t.TokenHash == "h1").RevokedAt.Should().Be(firstRevokeTime);
        user.RefreshTokens.Single(t => t.TokenHash == "h2").RevokedAt.Should().Be(bulkRevokeTime);
        user.RefreshTokens.Single(t => t.TokenHash == "h3").RevokedAt.Should().Be(bulkRevokeTime);
    }
}
