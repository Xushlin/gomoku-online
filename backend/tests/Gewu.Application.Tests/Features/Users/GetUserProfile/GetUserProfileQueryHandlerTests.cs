using Gomoku.Application.Features.Users.GetUserProfile;
using Gomoku.Application.Tests.Features.Rooms;

namespace Gomoku.Application.Tests.Features.Users.GetUserProfile;

public class GetUserProfileQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private GetUserProfileQueryHandler Build() => new(_users.Object);

    [Fact]
    public async Task Success_Returns_Public_Dto_Without_Email()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        _users.Setup(r => r.FindByIdAsync(alice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alice);

        var dto = await Build().Handle(new GetUserProfileQuery(alice.Id), default);

        dto.Id.Should().Be(alice.Id.Value);
        dto.Username.Should().Be("Alice");
        dto.Rating.Should().Be(1200);
        dto.GamesPlayed.Should().Be(0);

        // Reflection: DTO 不暴露 Email / PasswordHash / RefreshTokens / IsActive / IsBot
        var propNames = typeof(UserPublicProfileDto).GetProperties().Select(p => p.Name).ToArray();
        propNames.Should().NotContain("Email");
        propNames.Should().NotContain("PasswordHash");
        propNames.Should().NotContain("RefreshTokens");
        propNames.Should().NotContain("IsActive");
        propNames.Should().NotContain("IsBot");
    }

    [Fact]
    public async Task Bot_Is_Also_Returnable()
    {
        var bot = RoomsFixtures.NewBot(BotDifficulty.Hard);
        _users.Setup(r => r.FindByIdAsync(bot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bot);

        var dto = await Build().Handle(new GetUserProfileQuery(bot.Id), default);

        dto.Username.Should().Be("AI_Hard");
        dto.Rating.Should().Be(1200);
    }

    [Fact]
    public async Task Unknown_User_Throws()
    {
        var unknownId = UserId.NewId();
        _users.Setup(r => r.FindByIdAsync(unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => Build().Handle(new GetUserProfileQuery(unknownId), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }
}
