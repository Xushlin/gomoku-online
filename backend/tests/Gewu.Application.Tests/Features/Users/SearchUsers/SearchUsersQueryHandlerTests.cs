using Gewu.Application.Features.Users.SearchUsers;
using Gewu.Application.Tests.Features.Rooms;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Tests.Features.Users.SearchUsers;

public class SearchUsersQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private SearchUsersQueryHandler Build() => new(_users.Object);

    /// <summary>
    /// 让 handler 的批量战绩查询返回给定的行。默认空 —— 没有战绩行的人照样出现在搜索结果里,
    /// 只是显示初始值;搜索的是"人",不是"上过榜的人"。
    /// </summary>
    private void SetupStats(params UserGameStats[] rows) =>
        _users.Setup(r => r.FindGameStatsForAsync(
                It.IsAny<IEnumerable<UserId>>(), GameKeys.Gomoku, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, UserGameStats>)
                rows.ToDictionary(s => s.UserId.Value));

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
        SetupStats();

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
        SetupStats();

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
        SetupStats();

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
        var stats = UserGameStats.Start(alice.Id, GameKeys.Gomoku);
        stats.RecordGameResult(GameOutcome.Win, 1220);
        stats.RecordGameResult(GameOutcome.Draw, 1220);
        _users.Setup(r => r.SearchByUsernamePagedAsync(
                null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)new[] { alice }, 1));
        SetupStats(stats);

        var result = await Build().Handle(new SearchUsersQuery(null, 1, 20), default);

        var entry = result.Items.Single();
        entry.Id.Should().Be(alice.Id.Value);
        entry.Rating.Should().Be(1220);
        entry.GamesPlayed.Should().Be(2);
        entry.Wins.Should().Be(1);
        entry.Draws.Should().Be(1);
    }

    [Fact]
    public async Task A_User_With_No_Gomoku_Row_Still_Appears_With_Initial_Values()
    {
        // 搜索的是"人",不是"上过榜的人"。找人卡片要能找到刚注册的人 ——
        // 排行榜的成员资格规则(没下过就不上榜)不适用于这里。
        var newbie = RoomsFixtures.NewUser("Newbie", "newbie@example.com");
        _users.Setup(r => r.SearchByUsernamePagedAsync(
                null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)new[] { newbie }, 1));
        SetupStats();

        var result = await Build().Handle(new SearchUsersQuery(null, 1, 20), default);

        var entry = result.Items.Single();
        entry.Username.Should().Be("Newbie");
        entry.Rating.Should().Be(1200);
        entry.GamesPlayed.Should().Be(0);
    }

    [Fact]
    public async Task Stats_Are_Fetched_In_One_Batch_Pinned_To_Gomoku()
    {
        // 一页 20 人,逐个查就是 20 次往返。另外:棋种钉死在 gomoku 且**不接受参数** ——
        // 找人卡片是五子棋大厅的组件,参数化它等于开始泛化大厅,那是单独的一步。
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        _users.Setup(r => r.SearchByUsernamePagedAsync(
                null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<User>)new[] { alice, bob }, 2));
        SetupStats();

        await Build().Handle(new SearchUsersQuery(null, 1, 20), default);

        _users.Verify(
            r => r.FindGameStatsForAsync(
                It.IsAny<IEnumerable<UserId>>(), GameKeys.Gomoku, It.IsAny<CancellationToken>()),
            Times.Once);
        _users.Verify(
            r => r.FindGameStatsAsync(
                It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
