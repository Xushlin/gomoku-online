using Gomoku.Application.Features.Users.GetLeaderboard;

namespace Gomoku.Application.Tests.Features.Users.GetLeaderboard;

public class GetLeaderboardQueryHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserRepository> _users = new();

    private GetLeaderboardQueryHandler Build() => new(_users.Object);

    private static User NewUser(string name, int rating, int wins, int losses, int draws)
    {
        var u = User.Register(
            UserId.NewId(),
            new Email($"{name.ToLowerInvariant()}@example.com"),
            new Username(name),
            "HASHED",
            FixedNow);

        for (var i = 0; i < wins; i++) u.RecordGameResult(GameOutcome.Win, rating);
        for (var i = 0; i < losses; i++) u.RecordGameResult(GameOutcome.Loss, rating);
        for (var i = 0; i < draws; i++) u.RecordGameResult(GameOutcome.Draw, rating);
        return u;
    }

    [Fact]
    public async Task Empty_Repository_Returns_Empty_PagedResult()
    {
        _users.Setup(r => r.GetLeaderboardPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)Array.Empty<User>(), 0));

        var result = await Build().Handle(new GetLeaderboardQuery(1, 20), default);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Assigns_Rank_Starting_From_One_On_Page_One()
    {
        var alice = NewUser("Alice", 1500, 5, 1, 0);
        var bob = NewUser("Bob", 1400, 3, 2, 0);
        var carol = NewUser("Carol", 1300, 2, 5, 1);
        _users.Setup(r => r.GetLeaderboardPagedAsync(
                1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)new List<User> { alice, bob, carol }, 3));

        var result = await Build().Handle(new GetLeaderboardQuery(1, 20), default);

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
        var ev = NewUser("Eve", 1200, 0, 3, 0);
        var fr = NewUser("Fran", 1100, 0, 4, 0);
        _users.Setup(r => r.GetLeaderboardPagedAsync(
                2, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)new List<User> { ev, fr }, 5));

        var result = await Build().Handle(new GetLeaderboardQuery(2, 2), default);

        result.Total.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.Items[0].Rank.Should().Be(3); // (2-1)*2 + 0 + 1
        result.Items[1].Rank.Should().Be(4);
    }

    [Fact]
    public async Task Maps_All_Public_Fields()
    {
        var alice = NewUser("Alice", 1500, 5, 1, 2);
        _users.Setup(r => r.GetLeaderboardPagedAsync(
                1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)new List<User> { alice }, 1));

        var result = await Build().Handle(new GetLeaderboardQuery(1, 20), default);

        var entry = result.Items.Single();
        entry.UserId.Should().Be(alice.Id.Value);
        entry.Username.Should().Be("Alice");
        entry.Rating.Should().Be(1500);
        entry.GamesPlayed.Should().Be(8);
        entry.Wins.Should().Be(5);
        entry.Losses.Should().Be(1);
        entry.Draws.Should().Be(2);
    }

    [Fact]
    public async Task Passes_Page_And_PageSize_To_Repository()
    {
        _users.Setup(r => r.GetLeaderboardPagedAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)Array.Empty<User>(), 0));

        await Build().Handle(new GetLeaderboardQuery(3, 50), default);

        _users.Verify(r => r.GetLeaderboardPagedAsync(3, 50, It.IsAny<CancellationToken>()), Times.Once);
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
