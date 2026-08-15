using Gewu.Application.Features.Users.GetLeaderboard;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Tests.Features.Users.GetLeaderboard;

public class GetLeaderboardQueryHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserRepository> _users = new();

    private GetLeaderboardQueryHandler Build() => new(_users.Object);

    /// <summary>
    /// 造一位玩家 + 他在某棋种上的战绩行,并把用户名查询也接上 —— handler 现在从仓储拿
    /// <c>UserGameStats</c>,用户名走 <c>LookupUsernamesAsync</c> 另取(内部逐个 FindByIdAsync)。
    /// </summary>
    private (User User, UserGameStats Stats) NewEntry(
        string name, int rating, int wins, int losses, int draws, string gameKey = GameKeys.Gomoku)
    {
        var user = User.Register(
            UserId.NewId(),
            new Email($"{name.ToLowerInvariant()}@example.com"),
            new Username(name),
            "HASHED",
            FixedNow);

        var stats = UserGameStats.Start(user.Id, gameKey);
        for (var i = 0; i < wins; i++) stats.RecordGameResult(GameOutcome.Win, rating);
        for (var i = 0; i < losses; i++) stats.RecordGameResult(GameOutcome.Loss, rating);
        for (var i = 0; i < draws; i++) stats.RecordGameResult(GameOutcome.Draw, rating);

        _users.Setup(r => r.FindByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        return (user, stats);
    }

    private void SetupPage(string gameKey, int page, int pageSize, int total, params UserGameStats[] rows) =>
        _users.Setup(r => r.GetLeaderboardPagedAsync(
                gameKey, page, pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<UserGameStats>)rows.ToList(), total));

    private void SetupAnyPageEmpty() =>
        _users.Setup(r => r.GetLeaderboardPagedAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<UserGameStats>)Array.Empty<UserGameStats>(), 0));

    [Fact]
    public async Task Empty_Repository_Returns_Empty_PagedResult()
    {
        SetupAnyPageEmpty();

        var result = await Build().Handle(new GetLeaderboardQuery(GameKeys.Gomoku, 1, 20), default);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Assigns_Rank_Starting_From_One_On_Page_One()
    {
        var alice = NewEntry("Alice", 1500, 5, 1, 0);
        var bob = NewEntry("Bob", 1400, 3, 2, 0);
        var carol = NewEntry("Carol", 1300, 2, 5, 1);
        SetupPage(GameKeys.Gomoku, 1, 20, 3, alice.Stats, bob.Stats, carol.Stats);

        var result = await Build().Handle(new GetLeaderboardQuery(GameKeys.Gomoku, 1, 20), default);

        result.Items.Should().HaveCount(3);
        result.Total.Should().Be(3);
        result.Items[0].Rank.Should().Be(1);
        result.Items[0].Username.Should().Be("Alice");
        result.Items[1].Rank.Should().Be(2);
        result.Items[1].Username.Should().Be("Bob");
        result.Items[2].Rank.Should().Be(3);
        result.Items[2].Username.Should().Be("Carol");
    }

    [Fact]
    public async Task Rank_On_Page_Two_Is_Global_Not_Page_Local()
    {
        // 模拟:total=5,page=2 pageSize=2,仓储返回两条(全局第 3、4 名)
        var ev = NewEntry("Eve", 1200, 0, 3, 0);
        var fr = NewEntry("Fran", 1100, 0, 4, 0);
        SetupPage(GameKeys.Gomoku, 2, 2, 5, ev.Stats, fr.Stats);

        var result = await Build().Handle(new GetLeaderboardQuery(GameKeys.Gomoku, 2, 2), default);

        result.Total.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.Items[0].Rank.Should().Be(3); // (2-1)*2 + 0 + 1
        result.Items[1].Rank.Should().Be(4);
    }

    [Fact]
    public async Task Maps_All_Public_Fields()
    {
        var alice = NewEntry("Alice", 1500, 5, 1, 2);
        SetupPage(GameKeys.Gomoku, 1, 20, 1, alice.Stats);

        var result = await Build().Handle(new GetLeaderboardQuery(GameKeys.Gomoku, 1, 20), default);

        var entry = result.Items.Single();
        entry.UserId.Should().Be(alice.User.Id.Value);
        entry.Username.Should().Be("Alice");
        entry.Rating.Should().Be(1500);
        entry.GamesPlayed.Should().Be(8);
        entry.Wins.Should().Be(5);
        entry.Losses.Should().Be(1);
        entry.Draws.Should().Be(2);
    }

    [Fact]
    public async Task Passes_GameKey_Page_And_PageSize_To_Repository()
    {
        SetupAnyPageEmpty();

        await Build().Handle(new GetLeaderboardQuery("xiangqi", 3, 50), default);

        _users.Verify(
            r => r.GetLeaderboardPagedAsync("xiangqi", 3, 50, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task An_Unregistered_Game_Key_Returns_An_Empty_Board_Not_An_Error()
    {
        // 集合端点上"这个棋种没有榜"与"榜是空的"对调用方无从分辨 —— 报错会把前者说成客户端错了。
        // 一个新棋种刚上线时它的榜几乎是空的,那是**对的**。
        SetupAnyPageEmpty();

        var result = await Build().Handle(new GetLeaderboardQuery("a-game-nobody-registered", 1, 20), default);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public void Dto_Does_Not_Expose_Sensitive_Fields()
    {
        var props = typeof(LeaderboardEntryDto).GetProperties().Select(p => p.Name).ToArray();

        props.Should().BeEquivalentTo(new[]
        {
            nameof(LeaderboardEntryDto.Rank),
            nameof(LeaderboardEntryDto.UserId),
            nameof(LeaderboardEntryDto.Username),
            nameof(LeaderboardEntryDto.Rating),
            nameof(LeaderboardEntryDto.GamesPlayed),
            nameof(LeaderboardEntryDto.Wins),
            nameof(LeaderboardEntryDto.Losses),
            nameof(LeaderboardEntryDto.Draws),
        });
    }
}
