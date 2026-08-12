using Gomoku.Application.Features.Auth.Logout;
using MediatR;

namespace Gomoku.Application.Tests.Features.Auth.Logout;

public class LogoutCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IJwtTokenService> _tokens = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private LogoutCommandHandler BuildSut()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        return new LogoutCommandHandler(_users.Object, _tokens.Object, _clock.Object, _uow.Object);
    }

    private static User UserWithToken(string hash, DateTime? revokedAt = null)
    {
        var user = User.Register(
            UserId.NewId(),
            new Email("alice@example.com"),
            new Username("Alice"),
            "HASHED",
            Now.AddMinutes(-1));
        user.IssueRefreshToken(hash, Now.AddDays(7), Now.AddMinutes(-1));
        if (revokedAt.HasValue)
        {
            user.RevokeRefreshToken(hash, revokedAt.Value);
        }
        return user;
    }

    [Fact]
    public async Task Success_Revokes_And_Saves()
    {
        var user = UserWithToken("h");
        _tokens.Setup(t => t.HashRefreshToken("raw")).Returns("h");
        _users.Setup(r => r.FindByRefreshTokenHashAsync("h", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = BuildSut();
        var result = await sut.Handle(new LogoutCommand("raw"), default);

        result.Should().Be(Unit.Value);
        user.RefreshTokens.Single().RevokedAt.Should().Be(Now);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Token_Not_Found_Is_Silent_No_Save()
    {
        _tokens.Setup(t => t.HashRefreshToken("raw")).Returns("h");
        _users.Setup(r => r.FindByRefreshTokenHashAsync("h", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var sut = BuildSut();
        var result = await sut.Handle(new LogoutCommand("raw"), default);

        result.Should().Be(Unit.Value);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Already_Revoked_Does_Not_Overwrite_Timestamp()
    {
        var originalRevoke = Now.AddHours(-1);
        var user = UserWithToken("h", revokedAt: originalRevoke);
        _tokens.Setup(t => t.HashRefreshToken("raw")).Returns("h");
        _users.Setup(r => r.FindByRefreshTokenHashAsync("h", It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var sut = BuildSut();
        await sut.Handle(new LogoutCommand("raw"), default);

        user.RefreshTokens.Single().RevokedAt.Should().Be(originalRevoke);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_Token_Is_Silent(string? raw)
    {
        var sut = BuildSut();
        var result = await sut.Handle(new LogoutCommand(raw!), default);

        result.Should().Be(Unit.Value);
        _users.Verify(r => r.FindByRefreshTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
