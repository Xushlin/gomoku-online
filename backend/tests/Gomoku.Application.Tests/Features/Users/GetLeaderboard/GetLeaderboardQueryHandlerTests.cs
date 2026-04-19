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
    public async Task Empty_Repository_Returns_Empty_List()
    {
        _users.Setup(r => r.GetTopByRatingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        var result = await Build().Handle(new GetLeaderboardQuery(), default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Assigns_Rank_In_Repository_Returned_Order_Starting_From_One()
    {
        var alice = NewUser("Alice", 1500, 5, 1, 0);
        var bob = NewUser("Bob", 1400, 3, 2, 0);
        var carol = NewUser("Carol", 1300, 2, 5, 1);
        _users.Setup(r => r.GetTopByRatingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { alice, bob, carol });

        var result = await Build().Handle(new GetLeaderboardQuery(), default);

        result.Should().HaveCount(3);
        result[0].Rank.Should().Be(1);
        result[0].Username.Should().Be("Alice");
        result[1].Rank.Should().Be(2);
        result[1].Username.Should().Be("Bob");
        result[2].Rank.Should().Be(3);
        result[2].Username.Should().Be("Carol");
    }

    [Fact]
    public async Task Maps_All_Public_Fields()
    {
        var alice = NewUser("Alice", 1500, 5, 1, 2);
        _users.Setup(r => r.GetTopByRatingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { alice });

        var result = await Build().Handle(new GetLeaderboardQuery(), default);

        var entry = result.Single();
        entry.UserId.Should().Be(alice.Id.Value);
        entry.Username.Should().Be("Alice");
        entry.Rating.Should().Be(1500);
        entry.GamesPlayed.Should().Be(8);
        entry.Wins.Should().Be(5);
        entry.Losses.Should().Be(1);
        entry.Draws.Should().Be(2);
    }

    [Fact]
    public async Task Requests_LeaderboardSize_From_Repository()
    {
        _users.Setup(r => r.GetTopByRatingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        await Build().Handle(new GetLeaderboardQuery(), default);

        _users.Verify(r => r.GetTopByRatingAsync(100, It.IsAny<CancellationToken>()), Times.Once);
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
