using Gewu.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gewu.Infrastructure.Tests.Puzzles;

/// <summary>
/// 关卡种子载入。与 <c>IdiomSeeder</c> 同一契约:表里已有本游戏的关卡就直接返回,
/// 幂等性以 <c>(GameKey, LevelIndex)</c> 判定。
/// </summary>
public sealed class PuzzleLevelSeederTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private string _artefactPath = null!;

    private const string Artefact = """
    {
      "game": "idiom-crossword",
      "seed": 20260813,
      "dictionaryCommit": "fe6d6c2e8baa82187f4c96bbe042e43f96c05666",
      "levels": [
        {
          "levelIndex": 0,
          "difficulty": 1,
          "layout": {"rows":4,"cols":4,"cells":[{"row":0,"col":0}],"given":[],"tray":["合"],"slots":[]},
          "solution": {"cells":{"0,0":"合"},"words":[]}
        },
        {
          "levelIndex": 1,
          "difficulty": 2,
          "layout": {"rows":5,"cols":5,"cells":[{"row":0,"col":0}],"given":[],"tray":["一"],"slots":[]},
          "solution": {"cells":{"0,0":"一"},"words":[]}
        }
      ]
    }
    """;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        _artefactPath = Path.Combine(Path.GetTempPath(), $"crossword-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(_artefactPath, Artefact);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        if (File.Exists(_artefactPath))
        {
            File.Delete(_artefactPath);
        }
    }

    /// <summary>成语纵横的 seeder。游戏键与产物路径现在是构造参数。</summary>
    private PuzzleLevelSeeder Seeder(string gameKey = "idiom-crossword")
        => new(gameKey, PuzzleLevelSeeder.IdiomCrosswordPath, _db,
            NullLogger<PuzzleLevelSeeder>.Instance);

    [Fact]
    public async Task Seeds_an_empty_database()
    {
        await Seeder().SeedAsync(_artefactPath);

        var levels = await _db.PuzzleLevels
            .Where(l => l.GameKey == "idiom-crossword")
            .OrderBy(l => l.LevelIndex)
            .ToListAsync();

        levels.Should().HaveCount(2);
        levels[0].LevelIndex.Should().Be(0);
        levels[0].Difficulty.Should().Be(1);
        levels[0].LayoutJson.Should().Contain("\"rows\":4");
        levels[0].SolutionJson.Should().Contain("合");
        levels[1].LevelIndex.Should().Be(1);
    }

    [Fact]
    public async Task Is_idempotent_across_two_runs()
    {
        await Seeder().SeedAsync(_artefactPath);
        var afterFirst = await _db.PuzzleLevels.CountAsync();

        await Seeder().SeedAsync(_artefactPath);
        var afterSecond = await _db.PuzzleLevels.CountAsync();

        afterSecond.Should().Be(afterFirst);
        afterSecond.Should().Be(2);
    }

    [Fact]
    public async Task A_missing_artefact_is_a_warning_not_a_crash()
    {
        // 缺产物不该让应用起不来:开发者可能只想跑别的游戏。表现是关卡列表为空。
        var act = async () => await Seeder().SeedAsync(
            Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json"));

        await act.Should().NotThrowAsync();
        (await _db.PuzzleLevels.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task An_artefact_for_the_wrong_game_is_rejected()
    {
        var wrong = Path.Combine(Path.GetTempPath(), $"wrong-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(wrong, Artefact.Replace("idiom-crossword", "klotski"));

        try
        {
            var act = async () => await Seeder().SeedAsync(wrong);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .Where(e => e.Message.Contains("klotski"));
        }
        finally
        {
            File.Delete(wrong);
        }
    }

    [Fact]
    public async Task The_committed_artefact_seeds_cleanly()
    {
        // 打真产物,而不是只打测试夹具 —— 提交进仓库的那份必须真的能载入。
        var committed = Path.Combine(AppContext.BaseDirectory, PuzzleLevelSeeder.IdiomCrosswordPath);
        if (!File.Exists(committed))
        {
            Assert.Fail($"committed level artefact missing at {committed}");
        }

        await Seeder().SeedAsync(committed);

        var levels = await _db.PuzzleLevels
            .Where(l => l.GameKey == "idiom-crossword")
            .ToListAsync();

        levels.Should().NotBeEmpty();
        levels.Select(l => l.LevelIndex).Should().OnlyHaveUniqueItems();
        levels.Should().OnlyContain(l =>
            l.LayoutJson.Length > 0 && l.SolutionJson.Length > 0);
    }

    [Fact]
    public async Task The_same_seeder_loads_a_second_game()
    {
        // 这个 seeder 之所以从 CrosswordLevelSeeder 改名而来,就是因为除了游戏键和
        // 路径之外它没有任何成语纵横专属的东西。这条用真的华容道产物证明那句话。
        var committed = Path.Combine(AppContext.BaseDirectory, PuzzleLevelSeeder.KlotskiPath);
        if (!File.Exists(committed))
        {
            Assert.Fail($"committed klotski artefact missing at {committed}");
        }

        var seeder = new PuzzleLevelSeeder(
            "klotski", PuzzleLevelSeeder.KlotskiPath, _db, NullLogger<PuzzleLevelSeeder>.Instance);
        await seeder.SeedAsync(committed);

        var levels = await _db.PuzzleLevels
            .Where(l => l.GameKey == "klotski")
            .OrderBy(l => l.LevelIndex)
            .ToListAsync();

        levels.Should().NotBeEmpty();
        levels.Select(l => l.LevelIndex).Should().OnlyHaveUniqueItems();
        levels.Should().OnlyContain(l => l.SolutionJson.Contains("minMoves"));
    }

    [Fact]
    public async Task A_seeder_only_touches_its_own_game()
    {
        // 幂等性按 (GameKey, LevelIndex) 判定,所以两个游戏的 seeder 互不阻塞:
        // 先灌了成语纵横,不该让华容道变成 no-op。
        await Seeder().SeedAsync(
            Path.Combine(AppContext.BaseDirectory, PuzzleLevelSeeder.IdiomCrosswordPath));

        var klotski = new PuzzleLevelSeeder(
            "klotski", PuzzleLevelSeeder.KlotskiPath, _db, NullLogger<PuzzleLevelSeeder>.Instance);
        await klotski.SeedAsync(
            Path.Combine(AppContext.BaseDirectory, PuzzleLevelSeeder.KlotskiPath));

        (await _db.PuzzleLevels.CountAsync(l => l.GameKey == "idiom-crossword"))
            .Should().BeGreaterThan(0);
        (await _db.PuzzleLevels.CountAsync(l => l.GameKey == "klotski"))
            .Should().BeGreaterThan(0);
    }
}
