using Gomoku.Application.Features.Auth.Login;
using Microsoft.Extensions.Options;

namespace Gomoku.Application.Tests.Features.Auth.Login;

public class LoginCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IJwtTokenService> _tokens = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly IOptions<JwtOptions> _jwt = Options.Create(new JwtOptions
    {
        SigningKey = "dummy",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenLifetimeDays = 7,
    });

    private LoginCommandHandler BuildSut()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        return new LoginCommandHandler(
            _users.Object, _hasher.Object, _tokens.Object, _clock.Object, _uow.Object, _jwt);
    }

    private static User MakeUser(bool isActive = true)
    {
        var user = User.Register(
            UserId.NewId(),
            new Email("alice@example.com"),
            new Username("Alice"),
            "HASHED",
            Now);

        if (!isActive)
        {
            // 通过反射把 IsActive 设为 false(测试辅助;生产代码不会走这条路径)
            typeof(User).GetProperty(nameof(User.IsActive))!
                .SetValue(user, false);
        }

        return user;
    }

    [Fact]
    public async Task Success_Issues_New_Tokens()
    {
        var user = MakeUser();
        _users.Setup(r => r.FindByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("Password1", user.PasswordHash)).Returns(true);
        _tokens.Setup(t => t.GenerateRefreshToken()).Returns("new-raw");
        _tokens.Setup(t => t.HashRefreshToken("new-raw")).Returns("new-hash");
        _tokens.Setup(t => t.GenerateAccessToken(user))
            .Returns(new AccessToken("jwt", Now.AddMinutes(15)));
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = BuildSut();
        var response = await sut.Handle(new LoginCommand("alice@example.com", "Password1"), default);

        response.AccessToken.Should().Be("jwt");
        response.RefreshToken.Should().Be("new-raw");
        user.RefreshTokens.Should().ContainSingle(t => t.TokenHash == "new-hash");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task User_Not_Found_Throws_InvalidCredentials()
    {
        _users.Setup(r => r.FindByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var sut = BuildSut();
        var act = () => sut.Handle(new LoginCommand("alice@example.com", "Password1"), default);

        var ex = await act.Should().ThrowAsync<InvalidCredentialsException>();
        ex.Which.Message.Should().Be("Email or password is incorrect.");
    }

    [Fact]
    public async Task Wrong_Password_Throws_InvalidCredentials_With_Same_Message()
    {
        var user = MakeUser();
        _users.Setup(r => r.FindByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrong", user.PasswordHash)).Returns(false);

        var sut = BuildSut();
        var act = () => sut.Handle(new LoginCommand("alice@example.com", "wrong"), default);

        var ex = await act.Should().ThrowAsync<InvalidCredentialsException>();
        ex.Which.Message.Should().Be("Email or password is incorrect.");
    }

    [Fact]
    public async Task Invalid_Email_Format_Throws_InvalidCredentials()
    {
        // 故意输入非法邮箱,handler 应把领域的 InvalidEmailException 转成同一条模糊错误
        var sut = BuildSut();
        var act = () => sut.Handle(new LoginCommand("not-an-email", "whatever"), default);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Inactive_User_Throws_UserNotActive()
    {
        var user = MakeUser(isActive: false);
        _users.Setup(r => r.FindByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("Password1", user.PasswordHash)).Returns(true);

        var sut = BuildSut();
        var act = () => sut.Handle(new LoginCommand("alice@example.com", "Password1"), default);

        await act.Should().ThrowAsync<UserNotActiveException>();
    }
}
