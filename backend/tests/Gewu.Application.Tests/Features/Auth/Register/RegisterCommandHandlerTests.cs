using Gomoku.Application.Features.Auth.Register;
using Microsoft.Extensions.Options;

namespace Gomoku.Application.Tests.Features.Auth.Register;

public class RegisterCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);
    private readonly Mock<IPasswordHasher> _hasher = new(MockBehavior.Strict);
    private readonly Mock<IJwtTokenService> _tokens = new(MockBehavior.Strict);
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly IOptions<JwtOptions> _jwt = Options.Create(new JwtOptions
    {
        Issuer = "gomoku-online",
        Audience = "gomoku-online-clients",
        SigningKey = "dummy",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenLifetimeDays = 7,
    });

    private RegisterCommandHandler BuildSut()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        return new RegisterCommandHandler(
            _users.Object, _hasher.Object, _tokens.Object, _clock.Object, _uow.Object, _jwt);
    }

    [Fact]
    public async Task Successful_Registration_Persists_User_And_Returns_AuthResponse()
    {
        _users.Setup(r => r.EmailExistsAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.UsernameExistsAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _hasher.Setup(h => h.Hash("Password1")).Returns("HASHED");
        _tokens.Setup(t => t.GenerateRefreshToken()).Returns("raw-refresh");
        _tokens.Setup(t => t.HashRefreshToken("raw-refresh")).Returns("hashed-refresh");
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>()))
            .Returns(new AccessToken("jwt-token", Now.AddMinutes(15)));
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = BuildSut();
        var response = await sut.Handle(new RegisterCommand("alice@example.com", "Alice", "Password1"), default);

        response.AccessToken.Should().Be("jwt-token");
        response.RefreshToken.Should().Be("raw-refresh");
        response.AccessTokenExpiresAt.Should().Be(Now.AddMinutes(15));
        response.User.Email.Should().Be("alice@example.com");
        response.User.Username.Should().Be("Alice");
        response.User.Rating.Should().Be(1200);
        response.User.GamesPlayed.Should().Be(0);
        response.User.CreatedAt.Should().Be(Now);

        _users.Verify(r => r.AddAsync(It.Is<User>(u =>
            u.Email.Value == "alice@example.com"
            && u.Username.Value == "Alice"
            && u.PasswordHash == "HASHED"
            && u.RefreshTokens.Count == 1
            && u.RefreshTokens.Single().TokenHash == "hashed-refresh"
            && u.RefreshTokens.Single().ExpiresAt == Now.AddDays(7)),
            It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Email_Already_Exists_Throws_And_Does_Not_Add()
    {
        _users.Setup(r => r.EmailExistsAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = BuildSut();

        var act = () => sut.Handle(new RegisterCommand("alice@example.com", "Alice", "Password1"), default);

        await act.Should().ThrowAsync<EmailAlreadyExistsException>();
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Username_Already_Exists_Throws_And_Does_Not_Add()
    {
        _users.Setup(r => r.EmailExistsAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.UsernameExistsAsync(It.IsAny<Username>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = BuildSut();

        var act = () => sut.Handle(new RegisterCommand("alice@example.com", "Alice", "Password1"), default);

        await act.Should().ThrowAsync<UsernameAlreadyExistsException>();
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
