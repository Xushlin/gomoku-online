using Gewu.Application.Features.Users.GetUserProfile;
using Gewu.Application.Tests.Features.Rooms;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Tests.Features.Users.GetUserProfile;

public class GetUserProfileQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private GetUserProfileQueryHandler Build() => new(_users.Object);

    private void SetupStats(UserGameStats? stats, UserId userId, string gameKey = GameKeys.Gomoku) =>
        _users.Setup(r => r.FindGameStatsAsync(userId, gameKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

    [Fact]
    public async Task Success_Returns_Public_Dto_Without_Email()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var stats = UserGameStats.Start(alice.Id, GameKeys.Gomoku);
        stats.RecordGameResult(GameOutcome.Win, 1220);
        _users.Setup(r => r.FindByIdAsync(alice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alice);
        SetupStats(stats, alice.Id);

        var dto = await Build().Handle(new GetUserProfileQuery(alice.Id, GameKeys.Gomoku), default);

        dto.Id.Should().Be(alice.Id.Value);
        dto.Username.Should().Be("Alice");
        dto.Rating.Should().Be(1220);
        dto.GamesPlayed.Should().Be(1);

        // Reflection: DTO 不暴露 Email / PasswordHash / RefreshTokens / IsActive / IsBot
        var propNames = typeof(UserPublicProfileDto).GetProperties().Select(p => p.Name).ToArray();
        propNames.Should().NotContain("Email");
        propNames.Should().NotContain("PasswordHash");
        propNames.Should().NotContain("RefreshTokens");
        propNames.Should().NotContain("IsActive");
        propNames.Should().NotContain("IsBot");
    }

    [Fact]
    public async Task Dto_Shape_Is_Unchanged_By_This_Change()
    {
        // 本变更的判据:已发布的 Web 客户端零改动。形状一个字节不变,变的只是数据来源。
        var propNames = typeof(UserPublicProfileDto).GetProperties().Select(p => p.Name).ToArray();

        propNames.Should().BeEquivalentTo(new[]
        {
            "Id", "Username", "Rating", "GamesPlayed", "Wins", "Losses", "Draws", "CreatedAt",
        });
    }

    [Fact]
    public async Task Bot_Is_Also_Returnable()
    {
        var bot = RoomsFixtures.NewBot(BotDifficulty.Hard);
        _users.Setup(r => r.FindByIdAsync(bot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bot);
        SetupStats(null, bot.Id);

        var dto = await Build().Handle(new GetUserProfileQuery(bot.Id, GameKeys.Gomoku), default);

        dto.Username.Should().Be("AI_Hard");
        dto.Rating.Should().Be(1200);
    }

    [Fact]
    public async Task Unknown_User_Throws()
    {
        var unknownId = UserId.NewId();
        _users.Setup(r => r.FindByIdAsync(unknownId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => Build().Handle(new GetUserProfileQuery(unknownId, GameKeys.Gomoku), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task A_User_Who_Never_Played_This_Game_Gets_Initial_Values_Not_A_404()
    {
        // "这个人存在但没下过这个棋种"是一个正常答案。404 会被前端误报成"用户不存在" ——
        // 而一个新棋种刚上线时,几乎每个人都处在这个状态。
        var alice = RoomsFixtures.NewUser("Alice");
        _users.Setup(r => r.FindByIdAsync(alice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alice);
        SetupStats(null, alice.Id, "xiangqi");

        var dto = await Build().Handle(new GetUserProfileQuery(alice.Id, "xiangqi"), default);

        dto.Rating.Should().Be(1200);
        dto.GamesPlayed.Should().Be(0);
        dto.Wins.Should().Be(0);
        dto.Losses.Should().Be(0);
        dto.Draws.Should().Be(0);
    }

    [Fact]
    public async Task Reading_A_Profile_Never_Creates_A_Stats_Row()
    {
        // 用 FindGameStatsAsync 而不是 get-or-create:一次 GET 请求把人凭空登记进某个棋种的
        // 排行榜,会把"下过"的含义变成"被人看过资料"。
        var alice = RoomsFixtures.NewUser("Alice");
        _users.Setup(r => r.FindByIdAsync(alice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alice);
        SetupStats(null, alice.Id, "xiangqi");

        await Build().Handle(new GetUserProfileQuery(alice.Id, "xiangqi"), default);

        _users.Verify(
            r => r.GetOrCreateGameStatsAsync(
                It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task The_Requested_Game_Key_Is_Passed_Through()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        _users.Setup(r => r.FindByIdAsync(alice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alice);
        SetupStats(null, alice.Id, "xiangqi");

        await Build().Handle(new GetUserProfileQuery(alice.Id, "xiangqi"), default);

        _users.Verify(
            r => r.FindGameStatsAsync(alice.Id, "xiangqi", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
