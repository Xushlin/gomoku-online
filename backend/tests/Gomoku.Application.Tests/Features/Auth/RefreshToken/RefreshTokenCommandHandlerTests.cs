using Gomoku.Application.Features.Auth.RefreshToken;
using Microsoft.Extensions.Options;

namespace Gomoku.Application.Tests.Features.Auth.RefreshToken;

public class RefreshTokenCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IJwtTokenService> _tokens = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly IOptions<JwtOptions> _jwt = Options.Create(new JwtOptions
    {
        SigningKey = "dummy",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenLifetimeDays = 7,
    });

    private RefreshTokenCommandHandler BuildSut()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        return new RefreshTokenCommandHandler(
            _users.Object, _tokens.Object, _clock.Object, _uow.Object, _jwt);
    }

    private static User UserWithToken(string hash, DateTime expiresAt, DateTime createdAt, DateTime? revokedAt = null)
    {
        var user = User.Register(
            UserId.NewId(),
            new Email("alice@example.com"),
            new Username("Alice"),
            "HASHED",
            createdAt);
        user.IssueRefreshToken(hash, expiresAt, createdAt);
        if (revokedAt.HasValue)
        {
            user.RevokeRefreshToken(hash, revokedAt.Value);
        }
        return user;
    }

    [Fact]
    public async Task Success_Rotates_Tokens()
    {
        var user = UserWithToken("old-hash", Now.AddDays(7), Now.AddMinutes(-1));
        _tokens.Setup(t => t.HashRefreshToken("raw-old")).Returns("old-hash");
        _users.Setup(r => r.FindByRefreshTokenHashAsync("old-hash", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tokens.Setup(t => t.GenerateRefreshToken()).Returns("raw-new");
        _tokens.Setup(t => t.HashRefreshToken("raw-new")).Returns("new-hash");
        _tokens.Setup(t => t.GenerateAccessToken(user))
            .Returns(new AccessToken("jwt-new", Now.AddMinutes(15)));
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = BuildSut();
        var response = await sut.Handle(new RefreshTokenCommand("raw-old"), default);

        response.AccessToken.Should().Be("jwt-new");
        response.RefreshToken.Should().Be("raw-new");

        user.RefreshTokens.Single(t => t.TokenHash == "old-hash").RevokedAt.Should().Be(Now);
        user.RefreshTokens.Should().Contain(t => t.TokenHash == "new-hash" && t.RevokedAt == null);
    }

    [Fact]
    public async Task Not_Found_Throws_Invalid()
    {
        _tokens.Setup(t => t.HashRefreshToken(It.IsAny<string>())).Returns("some-hash");
        _users.Setup(r => r.FindByRefreshTokenHashAsync("some-hash", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var sut = BuildSut();
        var act = () => sut.Handle(new RefreshTokenCommand("raw"), default);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task Already_Revoked_Throws_Invalid()
    {
        var user = UserWithToken("h", Now.AddDays(7), Now.AddMinutes(-1), revokedAt: Now.AddSeconds(-1));
        _tokens.Setup(t => t.HashRefreshToken("raw")).Returns("h");
        _users.Setup(r => r.FindByRefreshTokenHashAsync("h", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var sut = BuildSut();
        var act = () => sut.Handle(new RefreshTokenCommand("raw"), default);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task Expired_Throws_Invalid()
    {
        var user = UserWithToken("h", Now.AddSeconds(-1), Now.AddDays(-8));
        _tokens.Setup(t => t.HashRefreshToken("raw")).Returns("h");
        _users.Setup(r => r.FindByRefreshTokenHashAsync("h", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var sut = BuildSut();
        var act = () => sut.Handle(new RefreshTokenCommand("raw"), default);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }
}
