using Gomoku.Application.Features.Users.GetCurrentUser;

namespace Gomoku.Application.Tests.Features.Users.GetCurrentUser;

public class GetCurrentUserQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserRepository> _users = new();

    [Fact]
    public async Task Success_Returns_UserDto()
    {
        var user = User.Register(
            UserId.NewId(),
            new Email("alice@example.com"),
            new Username("Alice"),
            "HASHED",
            Now);
        _users.Setup(r => r.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var sut = new GetCurrentUserQueryHandler(_users.Object);
        var dto = await sut.Handle(new GetCurrentUserQuery(user.Id), default);

        dto.Id.Should().Be(user.Id.Value);
        dto.Email.Should().Be("alice@example.com");
        dto.Username.Should().Be("Alice");
        dto.Rating.Should().Be(1200);
        dto.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Not_Found_Throws_UserNotFound()
    {
        var id = UserId.NewId();
        _users.Setup(r => r.FindByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var sut = new GetCurrentUserQueryHandler(_users.Object);
        var act = () => sut.Handle(new GetCurrentUserQuery(id), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Inactive_User_Throws_UserNotActive()
    {
        var user = User.Register(
            UserId.NewId(),
            new Email("alice@example.com"),
            new Username("Alice"),
            "HASHED",
            Now);
        typeof(User).GetProperty(nameof(User.IsActive))!.SetValue(user, false);
        _users.Setup(r => r.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var sut = new GetCurrentUserQueryHandler(_users.Object);
        var act = () => sut.Handle(new GetCurrentUserQuery(user.Id), default);

        await act.Should().ThrowAsync<UserNotActiveException>();
    }
}
