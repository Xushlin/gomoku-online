using Gomoku.Application.Features.Users.SearchUsers;
using Gomoku.Application.Tests.Features.Rooms;

namespace Gomoku.Application.Tests.Features.Users.SearchUsers;

public class SearchUsersQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private SearchUsersQueryHandler Build() => new(_users.Object);

    [Fact]
    public async Task Empty_Search_Returns_All_Filter_Bots_Delegated_To_Repo()
    {
        // 仓储层负责过滤 bot;handler 只按仓储返回的顺序映射。
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var carol = RoomsFixtures.NewUser("Carol", "carol@example.com");
        _users.Setup(r => r.SearchByUsernamePagedAsync(
                null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)new[] { alice, bob, carol }, 3));

        var result = await Build().Handle(new SearchUsersQuery(null, 1, 20), default);

        result.Items.Should().HaveCount(3);
        result.Total.Should().Be(3);
        result.Items.Select(x => x.Username).Should().ContainInOrder("Alice", "Bob", "Carol");
    }

    [Fact]
    public async Task Prefix_Match_Passes_Through_To_Repo()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var aliceB = RoomsFixtures.NewUser("AliceB", "aliceb@example.com");
        _users.Setup(r => r.SearchByUsernamePagedAsync(
                "Ali", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)new[] { alice, aliceB }, 2));

        var result = await Build().Handle(new SearchUsersQuery("Ali", 1, 20), default);

        result.Items.Should().HaveCount(2);
        result.Items[0].Username.Should().Be("Alice");
    }

    [Fact]
    public async Task Pagination_Metadata_Passed_Through()
    {
        _users.Setup(r => r.SearchByUsernamePagedAsync(
                null, 2, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)Array.Empty<User>(), 5));

        var result = await Build().Handle(new SearchUsersQuery(null, 2, 2), default);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(5);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Dto_Fields_Mapped_Correctly()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        alice.RecordGameResult(GameOutcome.Win, 1220);
        alice.RecordGameResult(GameOutcome.Draw, 1220);
        _users.Setup(r => r.SearchByUsernamePagedAsync(
                null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)new[] { alice }, 1));

        var result = await Build().Handle(new SearchUsersQuery(null, 1, 20), default);

        var entry = result.Items.Single();
        entry.Id.Should().Be(alice.Id.Value);
        entry.Rating.Should().Be(1220);
        entry.GamesPlayed.Should().Be(2);
        entry.Wins.Should().Be(1);
        entry.Draws.Should().Be(1);
    }
}
