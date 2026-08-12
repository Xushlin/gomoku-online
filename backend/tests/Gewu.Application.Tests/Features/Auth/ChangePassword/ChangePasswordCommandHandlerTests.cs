using Gomoku.Application.Features.Auth.ChangePassword;

namespace Gomoku.Application.Tests.Features.Auth.ChangePassword;

public class ChangePasswordCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 4, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private ChangePasswordCommandHandler Build()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        return new ChangePasswordCommandHandler(_users.Object, _hasher.Object, _clock.Object, _uow.Object);
    }

    private static User MakeUser() => User.Register(
        UserId.NewId(),
        new Email("alice@example.com"),
        new Username("Alice"),
        "oldhash",
        Now);

    [Fact]
    public async Task Success_Verifies_Hashes_Changes_RevokesAll_Saves()
    {
        var user = MakeUser();
        user.IssueRefreshToken("r1", Now.AddDays(7), Now);
        user.IssueRefreshToken("r2", Now.AddDays(7), Now);

        _users.Setup(r => r.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("old-plain", "oldhash")).Returns(true);
        _hasher.Setup(h => h.Hash("new-plain-123")).Returns("newhash");

        await Build().Handle(new ChangePasswordCommand(user.Id, "old-plain", "new-plain-123"), default);

        // Verify called once
        _hasher.Verify(h => h.Verify("old-plain", "oldhash"), Times.Once);
        // Password hash changed
        user.PasswordHash.Should().Be("newhash");
        // All refresh tokens revoked
        user.RefreshTokens.Should().AllSatisfy(t => t.RevokedAt.Should().NotBeNull());
        user.RefreshTokens.Should().AllSatisfy(t => t.RevokedAt.Should().Be(Now));
        // Save changes once
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Wrong_Current_Password_Throws_InvalidCredentials_No_Write()
    {
        var user = MakeUser();
        user.IssueRefreshToken("r1", Now.AddDays(7), Now);

        _users.Setup(r => r.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrong", "oldhash")).Returns(false);

        var act = () => Build().Handle(new ChangePasswordCommand(user.Id, "wrong", "new-plain-123"), default);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        user.PasswordHash.Should().Be("oldhash");
        user.RefreshTokens.Single().RevokedAt.Should().BeNull(); // 未 revoke
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task User_Not_Found_Throws_UserNotFound()
    {
        var unknownId = UserId.NewId();
        _users.Setup(r => r.FindByIdAsync(unknownId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var act = () => Build().Handle(new ChangePasswordCommand(unknownId, "any", "new-plain-123"), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Bot_User_Propagates_Domain_InvalidOperation()
    {
        var bot = User.RegisterBot(
            UserId.NewId(),
            new Email("easy@bot.gomoku.local"),
            new Username("AI_Easy"),
            Now);

        _users.Setup(r => r.FindByIdAsync(bot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(bot);
        // 理论上 bot 不可能登录,但防御路径:假设 JWT sub 指向 bot + Verify 意外通过(mock 返回 true)
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("newhash");

        var act = () => Build().Handle(new ChangePasswordCommand(bot.Id, "any", "new-plain-123"), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Bot accounts cannot change password*");
    }
}
