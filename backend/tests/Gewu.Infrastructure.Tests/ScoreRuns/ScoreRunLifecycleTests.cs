using FluentAssertions;
using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Features.ScoreRuns.GetScoreLeaderboard;
using Gewu.Application.Features.ScoreRuns.StartScoreRun;
using Gewu.Application.Features.ScoreRuns.SubmitScoreRun;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Tetris;
using Gewu.Domain.ScoreRuns;
using Gewu.Domain.Users;
using Gewu.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Tests.ScoreRuns;

/// <summary>
/// 计分类 run 的端到端行为,handler 打真 SQLite。
/// <para>
/// 必须在这一层测榜:每人一行是一次 SQL 分组去重,而 Application 层的仓储 mock 无论传什么
/// 都会返回同一批数据 —— 那里能证明的只有「handler 把窗口算出来传下去了」。
/// **谓词和去重有没有真生效,只有真 SQL 说得清**,这也是那条相关子查询写在这里被验的理由:
/// 一旦它退化成客户端求值,过滤与分页就都搬进了进程,而结果照样是对的。
/// </para>
/// </summary>
public sealed class ScoreRunLifecycleTests : IAsyncLifetime
{
    // 2026-08-19 是周三 —— 本自然周从 08-17(周一)00:00 UTC 起。
    private static readonly DateTime Wednesday = new(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc);
    private const int Seed = 20260818;

    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private ScoreRunRepository _repo = null!;
    private UserRepository _users = null!;
    private FakeClock _clock = null!;
    private FakeSeeds _seeds = null!;
    private IUnitOfWork _uow = null!;

    private User _alice = null!;
    private User _bob = null!;

    private sealed class FakeClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = Wednesday;
    }

    private sealed class FakeSeeds : ISeedProvider
    {
        public int Next { get; set; } = Seed;
        public int Calls { get; private set; }
        public int NextSeed() { Calls++; return Next; }
    }

    private sealed class DbUnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _db;
        public DbUnitOfWork(AppDbContext db) => _db = db;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _db.SaveChangesAsync(cancellationToken);
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        _alice = NewUser("Alice");
        _bob = NewUser("Bob");
        _db.Users.AddRange(_alice, _bob);
        await _db.SaveChangesAsync();

        _repo = new ScoreRunRepository(_db);
        _users = new UserRepository(_db);
        _clock = new FakeClock();
        _seeds = new FakeSeeds();
        _uow = new DbUnitOfWork(_db);
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
        Wednesday);

    // ---- helpers ----

    private Task<ScoreRunStartedDto> Start(UserId who, string gameKey = TetrisRules.GameKey)
        => new StartScoreRunCommandHandler(_repo, _seeds, _clock, _uow)
            .Handle(new StartScoreRunCommand(who, gameKey), default);

    private Task<ScoreRunResultDto> Submit(UserId who, Guid runId, IEnumerable<TetrisPlacement> ps)
        => new SubmitScoreRunCommandHandler(_repo, _clock, _uow)
            .Handle(
                new SubmitScoreRunCommand(
                    who, runId, ps.Select(p => new ScorePlacementDto(p.Rotation, p.Column)).ToList()),
                default);

    private Task<PagedResult<ScoreLeaderboardEntryDto>> Board(
        ScoreWindow window = ScoreWindow.Week, int page = 1, int pageSize = 20)
        => new GetScoreLeaderboardQueryHandler(_repo, _users, _clock)
            .Handle(
                new GetScoreLeaderboardQuery(TetrisRules.GameKey, window, page, pageSize), default);

    /// <summary>最低优先贪心 —— 只要求「真的消到行」,与 Domain 测里那个同法。</summary>
    private static IReadOnlyList<TetrisPlacement> GreedySweep(int seed, int pieces)
    {
        var kinds = TetrisPieceSequence.Take(seed, pieces);
        var field = new TetrisField();
        var placements = new List<TetrisPlacement>(pieces);

        foreach (var kind in kinds)
        {
            var best = (Rotation: -1, Column: -1, Landing: -1);
            for (var rot = 0; rot < Tetromino.Rotations; rot++)
            {
                var width = Tetromino.WidthOf(kind, rot);
                for (var col = 0; col + width <= TetrisRules.Columns; col++)
                {
                    if (field.LandingRow(kind, rot, col) is int row && row > best.Landing)
                    {
                        best = (rot, col, row);
                    }
                }
            }

            if (best.Rotation < 0) break;
            field.PlaceAndClear(kind, best.Rotation, best.Column);
            placements.Add(new TetrisPlacement(best.Rotation, best.Column));
        }

        return placements;
    }

    /// <summary>把一局塞进库并直接结算 —— 造榜用,绕开 handler。</summary>
    private async Task<ScoreRun> Seeded(User who, int score, DateTime finishedAt)
    {
        var run = ScoreRun.Start(
            Guid.NewGuid(), who.Id, TetrisRules.GameKey, Seed, finishedAt.AddMinutes(-5));
        run.Finish(score, score / 100, 1, finishedAt);
        _db.ScoreRuns.Add(run);
        await _db.SaveChangesAsync();
        return run;
    }

    // ---- 生命周期 ----

    [Fact]
    public async Task Starting_a_run_hands_out_a_server_generated_seed_and_persists_it()
    {
        var started = await Start(_alice.Id);

        started.Seed.Should().Be(Seed);
        started.StartedAt.Should().Be(Wednesday);
        _seeds.Calls.Should().Be(1);

        var stored = await _db.ScoreRuns.SingleAsync();
        stored.Seed.Should().Be(Seed, "重放读的是库里那个种子,不是客户端手上那个");
        stored.FinishedAt.Should().BeNull();
        stored.Score.Should().BeNull();
    }

    [Fact]
    public async Task Starting_a_run_for_a_non_score_attack_game_is_a_404()
    {
        var act = () => Start(_alice.Id, "gomoku");

        await act.Should().ThrowAsync<ScoreRunNotFoundException>();
        (await _db.ScoreRuns.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task The_recorded_score_is_the_one_the_server_replayed()
    {
        var started = await Start(_alice.Id);
        var placements = GreedySweep(Seed, 200);
        var expected = TetrisRules.Replay(Seed, placements);
        expected.Score.Should().BePositive("这一局必须真的得分,否则这条什么都没验");

        _clock.UtcNow = Wednesday.AddMinutes(9);
        var result = await Submit(_alice.Id, started.RunId, placements);

        result.Score.Should().Be(expected.Score);
        result.Lines.Should().Be(expected.Lines);
        result.Level.Should().Be(expected.Level);
        result.Placements.Should().Be(placements.Count);
        // 用时取服务端时钟两端之差 —— 客户端上报的任何时间都进不来,命令里没有字段承载它。
        result.DurationMs.Should().Be((long)TimeSpan.FromMinutes(9).TotalMilliseconds);

        var stored = await _db.ScoreRuns.SingleAsync();
        stored.Score.Should().Be(expected.Score);
        stored.FinishedAt.Should().Be(Wednesday.AddMinutes(9));
    }

    [Fact]
    public async Task A_run_cannot_be_submitted_twice()
    {
        var started = await Start(_alice.Id);
        var placements = GreedySweep(Seed, 200);
        var first = await Submit(_alice.Id, started.RunId, placements);

        var act = () => Submit(_alice.Id, started.RunId, placements.Take(5).ToList());

        await act.Should().ThrowAsync<ScoreRunAlreadyFinishedException>();
        (await _db.ScoreRuns.SingleAsync()).Score.Should().Be(first.Score);
    }

    [Fact]
    public async Task Someone_elses_run_is_a_404_rather_than_a_403()
    {
        var started = await Start(_alice.Id);

        var act = () => Submit(_bob.Id, started.RunId, GreedySweep(Seed, 20));

        // 404 而不是 403 —— 403 会告诉调用方「这个 id 确实存在」。
        await act.Should().ThrowAsync<ScoreRunNotFoundException>();
    }

    [Fact]
    public async Task An_illegal_placement_rejects_the_run_and_leaves_it_unfinished()
    {
        var started = await Start(_alice.Id);
        // 全部堆在同一列 —— 20 行的场地放不下 30 个方块。
        var tooTall = Enumerable.Repeat(new TetrisPlacement(0, 0), 30).ToList();

        var act = () => Submit(_alice.Id, started.RunId, tooTall);

        await act.Should().ThrowAsync<InvalidMoveException>();
        var stored = await _db.ScoreRuns.SingleAsync();
        // 先重放、后写入:提交失败不该把这一局的机会用掉。
        stored.FinishedAt.Should().BeNull();
        stored.Score.Should().BeNull();
    }

    // ---- 榜 ----

    [Fact]
    public async Task Each_player_occupies_one_row_the_best_one()
    {
        await Seeded(_alice, 500, Wednesday.AddHours(-2));
        await Seeded(_alice, 1500, Wednesday.AddHours(-1));
        await Seeded(_alice, 900, Wednesday);
        await Seeded(_bob, 1200, Wednesday);

        var board = await Board();

        board.Total.Should().Be(2, "两个玩家 = 两行,不是四行");
        board.Items.Select(i => i.Username).Should().Equal("Alice", "Bob");
        board.Items[0].Score.Should().Be(1500);
        board.Items[0].Rank.Should().Be(1);
        board.Items[1].Score.Should().Be(1200);
        board.Items[1].Rank.Should().Be(2);
    }

    [Fact]
    public async Task A_run_that_finished_before_this_monday_is_not_on_the_week_board()
    {
        // 本周一是 08-17 00:00 UTC。这一局结束于它之前一秒 —— 距今不到 7 天,
        // 所以一个滚动 7 天的实现会把它留在榜上,而自然周必须把它切掉。
        await Seeded(_alice, 5000, new DateTime(2026, 8, 16, 23, 59, 59, DateTimeKind.Utc));
        await Seeded(_bob, 100, Wednesday);

        var week = await Board();

        week.Items.Should().ContainSingle().Which.Username.Should().Be("Bob");
    }

    [Fact]
    public async Task Exactly_monday_midnight_is_inside_the_week()
    {
        await Seeded(_alice, 5000, new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc));

        (await Board()).Items.Should().ContainSingle().Which.Score.Should().Be(5000);
    }

    [Fact]
    public async Task All_does_not_filter_by_time()
    {
        await Seeded(_alice, 5000, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await Seeded(_bob, 100, Wednesday);

        var all = await Board(ScoreWindow.All);

        all.Total.Should().Be(2);
        all.Items[0].Score.Should().Be(5000);
    }

    [Fact]
    public async Task An_unfinished_run_is_not_on_any_board()
    {
        await Start(_alice.Id);

        (await Board(ScoreWindow.All)).Total.Should().Be(0);
    }

    [Fact]
    public async Task Rank_is_global_across_pages()
    {
        await Seeded(_alice, 900, Wednesday);
        await Seeded(_bob, 800, Wednesday);

        var second = await Board(ScoreWindow.Week, page: 2, pageSize: 1);

        second.Items.Should().ContainSingle().Which.Rank.Should().Be(2);
        second.Items[0].Username.Should().Be("Bob");
        second.Total.Should().Be(2);
    }

    [Fact]
    public async Task A_tie_still_yields_one_row_per_player()
    {
        // 同分且同一时刻 —— 去重的比较必须是全序,否则同一个玩家会占两行。
        var t = Wednesday;
        await Seeded(_alice, 700, t);
        await Seeded(_alice, 700, t);

        (await Board()).Total.Should().Be(1);
    }

    [Fact]
    public async Task Another_games_runs_are_not_on_this_board()
    {
        var other = ScoreRun.Start(
            Guid.NewGuid(), _alice.Id, "some-other-score-game", Seed, Wednesday.AddMinutes(-5));
        other.Finish(9999, 90, 10, Wednesday);
        _db.ScoreRuns.Add(other);
        await _db.SaveChangesAsync();
        await Seeded(_bob, 100, Wednesday);

        var board = await Board(ScoreWindow.All);

        board.Items.Should().ContainSingle().Which.Username.Should().Be("Bob");
    }
}
