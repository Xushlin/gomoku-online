using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Users;
using Gewu.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>UserRepository</c> 上与分棋种战绩相关的四个方法,打真 SQLite。
/// <para>
/// 必须在这一层测:按棋种过滤、bot 过滤、排序、以及"get-or-create 不自行提交",
/// 全是 EF 谓词与变更跟踪的行为。Application 层的仓库 mock 无论传什么键都会返回同一批数据,
/// 那里能证明的只有"handler 把键传下去了"。**谓词有没有生效**只有真 SQL 说得清。
/// </para>
/// <para>
/// 用 in-memory SQLite 而不是 EF 的 InMemory provider,与 <c>RoomRepositoryGameKeyTests</c> 同理:
/// InMemory provider 会用 LINQ-to-Objects 假装成功,一个写错的 <c>Where</c> 照样过。
/// </para>
/// </summary>
public sealed class UserGameStatsRepositoryTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
    private const string Xiangqi = "xiangqi";

    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private UserRepository _repo = null!;

    private User _alice = null!;
    private User _bob = null!;
    private User _carol = null!;
    private User _bot = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        await _db.Database.EnsureCreatedAsync();

        _alice = NewUser("Alice");
        _bob = NewUser("Bob");
        _carol = NewUser("Carol");
        _bot = User.RegisterBot(
            UserId.NewId(), new Email("easy@bot.gewu.local"), new Username("AI_Easy"), Now);
        _db.Users.AddRange(_alice, _bob, _carol, _bot);

        // 五子棋:Alice 1500 / Bob 1400 / bot 1600(bot 分最高,正好检验它有没有被挡在榜外)。
        // 象棋:只有 Bob 下过 —— Alice 与 Carol 在象棋榜上不该出现。
        _db.UserGameStats.AddRange(
            Stats(_alice, GameKeys.Gomoku, rating: 1500, wins: 5, losses: 1),
            Stats(_bob, GameKeys.Gomoku, rating: 1400, wins: 3, losses: 3),
            Stats(_bot, GameKeys.Gomoku, rating: 1600, wins: 9, losses: 0),
            Stats(_bob, Xiangqi, rating: 1300, wins: 1, losses: 2));
        await _db.SaveChangesAsync();

        _repo = new UserRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static User NewUser(string name) => User.Register(
        UserId.NewId(),
        new Email($"{name.ToLowerInvariant()}@example.com"),
        new Username(name),
        "HASHED",
        Now);

    private static UserGameStats Stats(User user, string gameKey, int rating, int wins, int losses)
    {
        var row = UserGameStats.Start(user.Id, gameKey);
        for (var i = 0; i < wins; i++) row.RecordGameResult(GameOutcome.Win, rating);
        for (var i = 0; i < losses; i++) row.RecordGameResult(GameOutcome.Loss, rating);
        return row;
    }

    // ---- get-or-create ----

    [Fact]
    public async Task GetOrCreate_returns_the_existing_row_without_resetting_it()
    {
        var row = await _repo.GetOrCreateGameStatsAsync(_alice.Id, GameKeys.Gomoku, default);

        row.Rating.Should().Be(1500);
        row.Wins.Should().Be(5);
        row.GamesPlayed.Should().Be(6);
    }

    [Fact]
    public async Task GetOrCreate_creates_an_initial_row_for_a_game_never_played()
    {
        var row = await _repo.GetOrCreateGameStatsAsync(_alice.Id, Xiangqi, default);

        row.Rating.Should().Be(1200);
        row.GamesPlayed.Should().Be(0);
        row.GameKey.Should().Be(Xiangqi);
    }

    [Fact]
    public async Task GetOrCreate_does_not_commit_on_its_own()
    {
        // 新行要和对局结束的其它变更合并到同一事务 —— 仓库自行 SaveChanges 会把
        // "棋下完了但事务后来回滚了"变成"战绩已经算过了"。
        await _repo.GetOrCreateGameStatsAsync(_carol.Id, GameKeys.Gomoku, default);

        await using var fresh = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        var persisted = await fresh.UserGameStats
            .AnyAsync(s => s.UserId == _carol.Id && s.GameKey == GameKeys.Gomoku);

        persisted.Should().BeFalse();
    }

    [Fact]
    public async Task The_row_created_by_GetOrCreate_lands_once_the_caller_commits()
    {
        var row = await _repo.GetOrCreateGameStatsAsync(_carol.Id, GameKeys.Gomoku, default);
        row.RecordGameResult(GameOutcome.Win, 1216);
        await _db.SaveChangesAsync();

        await using var fresh = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        var persisted = await fresh.UserGameStats
            .SingleAsync(s => s.UserId == _carol.Id && s.GameKey == GameKeys.Gomoku);

        persisted.Rating.Should().Be(1216);
        persisted.Wins.Should().Be(1);
    }

    [Fact]
    public async Task Two_game_keys_for_the_same_player_are_two_rows()
    {
        var gomoku = await _repo.GetOrCreateGameStatsAsync(_bob.Id, GameKeys.Gomoku, default);
        var xiangqi = await _repo.GetOrCreateGameStatsAsync(_bob.Id, Xiangqi, default);

        gomoku.Rating.Should().Be(1400);
        xiangqi.Rating.Should().Be(1300);
    }

    // ---- 只读查询 ----

    [Fact]
    public async Task Find_returns_null_for_a_game_never_played_and_creates_nothing()
    {
        var row = await _repo.FindGameStatsAsync(_carol.Id, Xiangqi, default);

        row.Should().BeNull();
        _db.ChangeTracker.Entries<UserGameStats>()
            .Should().NotContain(e => e.State == EntityState.Added);
    }

    [Fact]
    public async Task FindFor_returns_only_the_requested_game_key()
    {
        var rows = await _repo.FindGameStatsForAsync(
            new[] { _alice.Id, _bob.Id, _carol.Id }, Xiangqi, default);

        rows.Should().ContainKey(_bob.Id.Value);
        rows.Should().NotContainKey(_alice.Id.Value);
        rows.Should().NotContainKey(_carol.Id.Value);
        rows[_bob.Id.Value].Rating.Should().Be(1300);
    }

    [Fact]
    public async Task FindFor_on_an_empty_id_list_does_not_hit_the_database()
    {
        var rows = await _repo.FindGameStatsForAsync(Array.Empty<UserId>(), GameKeys.Gomoku, default);

        rows.Should().BeEmpty();
    }

    // ---- 排行榜 ----

    [Fact]
    public async Task The_leaderboard_is_isolated_per_game_key()
    {
        var (entries, total) = await _repo.GetLeaderboardPagedAsync(Xiangqi, 1, 20, default);

        total.Should().Be(1);
        entries.Single().UserId.Should().Be(_bob.Id);
        entries.Single().Rating.Should().Be(1300, "象棋那行是 1300,不是他五子棋的 1400");
    }

    [Fact]
    public async Task A_player_with_no_row_for_that_game_is_not_on_its_board()
    {
        // 备选是"所有人以 1200 分入榜"。那样一个从没下过象棋的人会出现在象棋榜上,
        // 位置取决于有多少人恰好也没下过 —— 榜的含义会从"棋力顺序"变成"谁碰巧下过"。
        var (entries, total) = await _repo.GetLeaderboardPagedAsync(Xiangqi, 1, 20, default);

        entries.Should().NotContain(e => e.UserId == _alice.Id);
        entries.Should().NotContain(e => e.UserId == _carol.Id);
        total.Should().Be(1);
    }

    [Fact]
    public async Task Bots_are_filtered_out_even_when_they_top_the_ratings()
    {
        // bot 分最高(1600)。它跟随 ELO 正常更新(反套利约束)但 MUST NOT 进榜 ——
        // 若过滤失效,它会排在第一位,是最显眼的失败方式。
        var (entries, total) = await _repo.GetLeaderboardPagedAsync(GameKeys.Gomoku, 1, 20, default);

        total.Should().Be(2);
        entries.Should().NotContain(e => e.UserId == _bot.Id);
        entries[0].UserId.Should().Be(_alice.Id);
        entries[1].UserId.Should().Be(_bob.Id);
    }

    [Fact]
    public async Task An_unregistered_game_key_yields_an_empty_board_not_an_error()
    {
        var (entries, total) = await _repo.GetLeaderboardPagedAsync("a-game-nobody-registered", 1, 20, default);

        entries.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task Paging_skips_within_the_filtered_set()
    {
        var (entries, total) = await _repo.GetLeaderboardPagedAsync(GameKeys.Gomoku, 2, 1, default);

        total.Should().Be(2, "Total 是过滤后的真人总数,不随分页变");
        entries.Should().HaveCount(1);
        entries.Single().UserId.Should().Be(_bob.Id);
    }

    [Fact]
    public async Task GamesPlayed_ascending_breaks_a_tie_on_rating_and_wins()
    {
        // 三级排序:分同、胜场同 → 场次少的排前。
        var dave = NewUser("Dave");
        var erin = NewUser("Erin");
        _db.Users.AddRange(dave, erin);
        _db.UserGameStats.AddRange(
            Stats(dave, Xiangqi, rating: 1300, wins: 1, losses: 2),
            Stats(erin, Xiangqi, rating: 1300, wins: 1, losses: 0));
        await _db.SaveChangesAsync();

        var (entries, _) = await _repo.GetLeaderboardPagedAsync(Xiangqi, 1, 20, default);

        entries.Select(e => e.UserId).Should().StartWith(new[] { erin.Id });
    }
}
