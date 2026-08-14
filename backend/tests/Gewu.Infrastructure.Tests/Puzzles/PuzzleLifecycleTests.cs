using System.Text.Json;
using Gewu.Application.Abstractions;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Features.Puzzles.CheckPuzzlePartial;
using Gewu.Application.Features.Puzzles.GetPuzzleLevel;
using Gewu.Application.Features.Puzzles.GetPuzzleLevels;
using Gewu.Application.Features.Puzzles.GetPuzzleProgress;
using Gewu.Application.Features.Puzzles.StartPuzzleAttempt;
using Gewu.Application.Features.Puzzles.SubmitPuzzleAttempt;
using Gewu.Application.Features.Puzzles.UsePuzzleHint;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Puzzles;
using Gewu.Domain.Users;
using Gewu.Infrastructure.Persistence.Repositories;
using Gewu.Infrastructure.Puzzles;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Tests.Puzzles;

/// <summary>
/// puzzle-core 的端到端行为,handler 打真 SQLite。
/// <para>
/// 本变更**不注册任何游戏**,所以这里注册一个假规则来跑通生命周期 —— 平台层的变更不该
/// 为了自测而塞进一个玩具游戏。假规则的答案是一个 <c>词 → 位置</c> 的简单映射,足以
/// 表达"完整校验 / 部分校验 / 提示 / 计分"四件事。
/// </para>
/// </summary>
public sealed class PuzzleLifecycleTests : IAsyncLifetime
{
    private const string GameKey = "fake-puzzle";
    private static readonly UserId Owner = new(Guid.NewGuid());
    private static readonly UserId Stranger = new(Guid.NewGuid());

    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private PuzzleRepository _repo = null!;
    private IPuzzleRulesRegistry _registry = null!;
    private FakeClock _clock = null!;
    private IUnitOfWork _uow = null!;

    /// <summary>把 "SOLVED" 当作唯一正确的完整答案,"OK-" 前缀当作正确的部分答案。</summary>
    private sealed class FakeRules : IPuzzleRules
    {
        public string GameKey => PuzzleLifecycleTests.GameKey;

        public PuzzleValidationResult Validate(string solutionJson, string submissionJson)
            => new(submissionJson == solutionJson);

        public PuzzlePartialResult CheckPartial(string solutionJson, string partialJson)
        {
            var correct = partialJson.StartsWith("OK-", StringComparison.Ordinal);

            // 答对时附带载荷,答错时故意也塞一个 —— 用来证明 handler 会把答错路径上的
            // 载荷丢掉,而不是原样转发(否则错误路径就成了泄题通道)。
            return correct
                ? new PuzzlePartialResult(true, "{\"note\":\"solved\"}")
                : new PuzzlePartialResult(false, "{\"leak\":\"must-not-reach-client\"}");
        }

        public PuzzleHintResult Hint(string solutionJson, string layoutJson, string? stateJson)
            => new($"{{\"state\":{(stateJson is null ? "null" : "\"seen\"")}}}");

        // 与原型同构:cost = 错误 + 提示;0 → 3 星,≤2 → 2 星,否则 1 星。
        public int Score(int hintsUsed, int mistakes, TimeSpan duration)
        {
            var cost = hintsUsed + mistakes;
            return cost == 0 ? 3 : cost <= 2 ? 2 : 1;
        }
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = new(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc);
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

        _db.PuzzleLevels.AddRange(
            PuzzleLevel.Create(GameKey, 0, 1, "{\"grid\":\"L0\"}", "SOLVED"),
            PuzzleLevel.Create(GameKey, 1, 1, "{\"grid\":\"L1\"}", "SOLVED"),
            PuzzleLevel.Create(GameKey, 2, 2, "{\"grid\":\"L2\"}", "SOLVED"));
        await _db.SaveChangesAsync();

        _repo = new PuzzleRepository(_db);
        _registry = new PuzzleRulesRegistry(new IPuzzleRules[] { new FakeRules() });
        _clock = new FakeClock();
        _uow = new DbUnitOfWork(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ---- helpers ----

    private Task<Application.Common.DTOs.PuzzleAttemptStartedDto> Start(int levelIndex, UserId? who = null)
        => new StartPuzzleAttemptCommandHandler(_repo, _registry, _clock, _uow)
            .Handle(new StartPuzzleAttemptCommand(who ?? Owner, GameKey, levelIndex), default);

    private Task<Application.Common.DTOs.PuzzleCheckResultDto> Check(Guid id, string partial, UserId? who = null)
        => new CheckPuzzlePartialCommandHandler(_repo, _registry, _uow)
            .Handle(new CheckPuzzlePartialCommand(who ?? Owner, id, partial), default);

    private Task<Application.Common.DTOs.PuzzleHintDto> Hint(Guid id, UserId? who = null)
        => new UsePuzzleHintCommandHandler(_repo, _registry, _uow)
            .Handle(new UsePuzzleHintCommand(who ?? Owner, id), default);

    private Task<Application.Common.DTOs.PuzzleSubmitResultDto> Submit(Guid id, string answer, UserId? who = null)
        => new SubmitPuzzleAttemptCommandHandler(_repo, _registry, _clock, _uow)
            .Handle(new SubmitPuzzleAttemptCommand(who ?? Owner, id, answer), default);

    private Task<Application.Common.DTOs.PuzzleProgressDto> Progress(UserId? who = null)
        => new GetPuzzleProgressQueryHandler(_repo, _registry)
            .Handle(new GetPuzzleProgressQuery(who ?? Owner, GameKey), default);

    // ---- tests ----

    [Fact]
    public async Task Full_lifecycle_scores_from_server_observed_signals()
    {
        var started = await Start(0);

        var wrong = await Check(started.AttemptId, "BAD-word");
        wrong.IsCorrect.Should().BeFalse();
        wrong.Mistakes.Should().Be(1);

        var right = await Check(started.AttemptId, "OK-word");
        right.IsCorrect.Should().BeTrue();
        right.Mistakes.Should().Be(1, "正确的部分校验不该计错");
        right.PayloadJson.Should().Be("{\"note\":\"solved\"}", "答对时载荷原样转发");

        // 答错路径 MUST NOT 转发载荷,即便规则实现填了 —— 否则未解开的部分会借错误路径泄漏。
        wrong.PayloadJson.Should().BeNull();

        var hint = await Hint(started.AttemptId);
        hint.HintsUsed.Should().Be(1);
        hint.RevealedJson.Should().Be("{\"state\":null}", "未带盘面状态时规则收到 null");

        _clock.UtcNow = _clock.UtcNow.AddSeconds(45);
        var result = await Submit(started.AttemptId, "SOLVED");

        result.IsCorrect.Should().BeTrue();
        result.Mistakes.Should().Be(1);
        result.HintsUsed.Should().Be(1);
        // cost = 1 + 1 = 2 → 2 星
        result.Stars.Should().Be(2);
        result.DurationMs.Should().Be(45_000);
        result.NewBest.Should().BeTrue();
    }

    [Fact]
    public async Task A_wrong_submission_records_a_mistake_and_leaves_the_attempt_open()
    {
        var started = await Start(0);

        var failed = await Submit(started.AttemptId, "NOPE");

        failed.IsCorrect.Should().BeFalse();
        failed.Stars.Should().BeNull();
        failed.Mistakes.Should().Be(1);

        // 尝试仍开启,可以继续改。
        var ok = await Submit(started.AttemptId, "SOLVED");
        ok.IsCorrect.Should().BeTrue();
        ok.Stars.Should().Be(2, "cost = 1 次错误 → 2 星");
    }

    [Fact]
    public async Task Resubmitting_a_finished_attempt_is_rejected()
    {
        var started = await Start(0);
        var first = await Submit(started.AttemptId, "SOLVED");
        first.Stars.Should().Be(3);

        var act = async () => await Submit(started.AttemptId, "SOLVED");

        await act.Should().ThrowAsync<AttemptAlreadyFinishedException>();
        var stored = await _db.PuzzleAttempts.SingleAsync(a => a.Id == started.AttemptId);
        stored.Stars.Should().Be(3);
    }

    [Fact]
    public async Task A_hint_after_submission_is_rejected()
    {
        var started = await Start(0);
        await Submit(started.AttemptId, "SOLVED");

        var act = async () => await Hint(started.AttemptId);

        await act.Should().ThrowAsync<AttemptAlreadyFinishedException>();
    }

    [Fact]
    public async Task Another_users_attempt_is_not_found()
    {
        var started = await Start(0);

        var check = async () => await Check(started.AttemptId, "OK-x", Stranger);
        var hint = async () => await Hint(started.AttemptId, Stranger);
        var submit = async () => await Submit(started.AttemptId, "SOLVED", Stranger);

        // 404 而不是 403 —— 不泄漏"这个 id 存在"。
        await check.Should().ThrowAsync<PuzzleNotFoundException>();
        await hint.Should().ThrowAsync<PuzzleNotFoundException>();
        await submit.Should().ThrowAsync<PuzzleNotFoundException>();
    }

    [Fact]
    public async Task Progress_is_derived_from_completed_levels()
    {
        foreach (var index in new[] { 0, 1, 2 })
        {
            var started = await Start(index);
            // 第 2 关故意用一次提示,拿 2 星,好让总星数不是简单的 3×3。
            if (index == 2)
            {
                await Hint(started.AttemptId);
            }
            await Submit(started.AttemptId, "SOLVED");
        }

        var progress = await Progress();

        progress.LevelsCompleted.Should().Be(3);
        progress.UnlockedLevelIndex.Should().Be(3);
        progress.TotalStars.Should().Be(3 + 3 + 2);
    }

    [Fact]
    public async Task Progress_starts_at_zero_for_a_new_player()
    {
        var progress = await Progress();

        progress.UnlockedLevelIndex.Should().Be(0);
        progress.TotalStars.Should().Be(0);
        progress.LevelsCompleted.Should().Be(0);
    }

    [Fact]
    public async Task Replaying_a_level_cannot_lower_the_recorded_best()
    {
        var first = await Start(0);
        await Submit(first.AttemptId, "SOLVED");

        var second = await Start(0);
        await Check(second.AttemptId, "BAD-1");
        await Check(second.AttemptId, "BAD-2");
        await Check(second.AttemptId, "BAD-3");
        var replay = await Submit(second.AttemptId, "SOLVED");

        replay.Stars.Should().Be(1);
        replay.NewBest.Should().BeFalse();

        var progress = await Progress();
        progress.TotalStars.Should().Be(3, "重玩变差不该拉低已有评级");
    }

    [Fact]
    public async Task Levels_unlock_one_at_a_time()
    {
        var handler = new GetPuzzleLevelsQueryHandler(_repo, _registry);

        var before = await handler.Handle(new GetPuzzleLevelsQuery(Owner, GameKey), default);
        before.Select(l => l.Unlocked).Should().Equal(true, false, false);

        var started = await Start(0);
        await Submit(started.AttemptId, "SOLVED");

        var after = await handler.Handle(new GetPuzzleLevelsQuery(Owner, GameKey), default);
        after.Select(l => l.Unlocked).Should().Equal(true, true, false);
        after[0].BestStars.Should().Be(3);
    }

    [Fact]
    public async Task An_unregistered_game_key_is_not_found_on_every_route()
    {
        var levels = new GetPuzzleLevelsQueryHandler(_repo, _registry);
        var level = new GetPuzzleLevelQueryHandler(_repo, _registry);

        var listAct = async () => await levels.Handle(new GetPuzzleLevelsQuery(Owner, "nope"), default);
        var getAct = async () => await level.Handle(new GetPuzzleLevelQuery("nope", 0), default);
        var startAct = async () => await Start(0, Owner) is null;
        var progressAct = async () => await new GetPuzzleProgressQueryHandler(_repo, _registry)
            .Handle(new GetPuzzleProgressQuery(Owner, "nope"), default);

        await listAct.Should().ThrowAsync<PuzzleNotFoundException>();
        await getAct.Should().ThrowAsync<PuzzleNotFoundException>();
        await progressAct.Should().ThrowAsync<PuzzleNotFoundException>();

        var startUnknown = async () => await new StartPuzzleAttemptCommandHandler(_repo, _registry, _clock, _uow)
            .Handle(new StartPuzzleAttemptCommand(Owner, "nope", 0), default);
        await startUnknown.Should().ThrowAsync<PuzzleNotFoundException>();
    }

    [Fact]
    public async Task A_missing_level_is_not_found()
    {
        var act = async () => await Start(99);

        await act.Should().ThrowAsync<PuzzleNotFoundException>();
    }

    [Fact]
    public async Task Game_key_and_level_index_are_unique()
    {
        _db.PuzzleLevels.Add(PuzzleLevel.Create(GameKey, 0, 1, "{}", "dup"));

        var act = async () => await _db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task The_level_dto_cannot_carry_the_solution()
    {
        // 答案封闭:布局会下发,答案不会。这里用一个可识别标记验证序列化结果。
        const string Marker = "SOLUTION-MARKER-DO-NOT-LEAK";
        _db.PuzzleLevels.Add(PuzzleLevel.Create("marker-game", 0, 1, "{\"grid\":\"visible\"}", Marker));
        await _db.SaveChangesAsync();

        var registry = new PuzzleRulesRegistry(new IPuzzleRules[] { new MarkerRules() });
        var dto = await new GetPuzzleLevelQueryHandler(_repo, registry)
            .Handle(new GetPuzzleLevelQuery("marker-game", 0), default);

        var json = JsonSerializer.Serialize(dto);
        json.Should().NotContain(Marker);
        json.Should().Contain("visible");
    }

    private sealed class MarkerRules : IPuzzleRules
    {
        public string GameKey => "marker-game";
        public PuzzleValidationResult Validate(string s, string x) => new(false);
        public PuzzlePartialResult CheckPartial(string s, string x) => new(false);
        public PuzzleHintResult Hint(string s, string l, string? state) => new("{}");
        public int Score(int h, int m, TimeSpan d) => 1;
    }
}
