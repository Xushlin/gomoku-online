using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gewu.Infrastructure.Tests.Idioms;

/// <summary>
/// <see cref="IdiomSeeder"/> 的行为:幂等、字符行展开正确、人工校订不被二次载入冲掉、
/// 以及产物层级与 <see cref="IdiomTiering"/> 不一致时**必须报错**而不是静默分叉。
/// </summary>
public sealed class IdiomSeederTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private IdiomSeeder _seeder = null!;
    private string _artefactPath = null!;

    // tier 1(四字 + 例句 + 出处 + 字频 500)与 tier 3(例句出处皆缺)各一条。
    private const string Artefact = """
    {
      "source": "https://github.com/pwxcoo/chinese-xinhua",
      "sourceCommit": "fe6d6c2e8baa82187f4c96bbe042e43f96c05666",
      "license": "MIT",
      "idioms": [
        {"word":"一举一动","pinyin":"yī jǔ yī dòng","minCharFrequency":500,"tier":1,"explanation":"每个动作。","derivation":"语出《朱子语类》","example":"他的～都被盯着。"},
        {"word":"闲花埜草","pinyin":"xián huā yě cǎo","minCharFrequency":3,"tier":3}
      ]
    }
    """;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        await _db.Database.EnsureCreatedAsync();

        _seeder = new IdiomSeeder(_db, NullLogger<IdiomSeeder>.Instance);

        _artefactPath = Path.Combine(Path.GetTempPath(), $"idioms-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(_artefactPath, Artefact);
    }

    public async Task DisposeAsync()
    {
        if (File.Exists(_artefactPath))
        {
            File.Delete(_artefactPath);
        }
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Seeding_an_empty_database_loads_every_row()
    {
        var written = await _seeder.SeedAsync(_artefactPath);

        written.Should().Be(2);
        (await _db.Idioms.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Seeding_expands_characters_with_matching_positions()
    {
        await _seeder.SeedAsync(_artefactPath);

        var chars = await _db.IdiomChars
            .Where(c => c.IdiomId == _db.Idioms.Single(i => i.Word == "一举一动").Id)
            .OrderBy(c => c.Position)
            .ToListAsync();

        chars.Select(c => c.Position).Should().Equal(0, 1, 2, 3);
        chars.Select(c => c.Char).Should().Equal('一', '举', '一', '动');
    }

    [Fact]
    public async Task Seeding_twice_is_a_no_op()
    {
        await _seeder.SeedAsync(_artefactPath);
        var idiomsAfterFirst = await _db.Idioms.CountAsync();
        var charsAfterFirst = await _db.IdiomChars.CountAsync();

        var written = await _seeder.SeedAsync(_artefactPath);

        written.Should().Be(0);
        (await _db.Idioms.CountAsync()).Should().Be(idiomsAfterFirst);
        (await _db.IdiomChars.CountAsync()).Should().Be(charsAfterFirst);
    }

    [Fact]
    public async Task A_manual_override_survives_a_second_seed()
    {
        await _seeder.SeedAsync(_artefactPath);
        var idiom = await _db.Idioms.SingleAsync(i => i.Word == "闲花埜草");
        idiom.OverrideTier(IdiomTier.Common);
        await _db.SaveChangesAsync();

        await _seeder.SeedAsync(_artefactPath);

        var reloaded = await _db.Idioms.SingleAsync(i => i.Word == "闲花埜草");
        reloaded.TierOverride.Should().Be(IdiomTier.Common);
        reloaded.EffectiveTier.Should().Be(IdiomTier.Common);
    }

    [Fact]
    public async Task A_missing_artefact_leaves_the_dictionary_empty_without_throwing()
    {
        var written = await _seeder.SeedAsync(Path.Combine(Path.GetTempPath(), "does-not-exist.json"));

        written.Should().Be(0);
        (await _db.Idioms.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task A_tier_the_domain_function_disagrees_with_is_rejected()
    {
        // 产物声称这条是 tier 1,但它没有例句也没有出处 —— IdiomTiering 会算成 3。
        // 与其静默采纳产物、让分层出现两份真源,不如直接失败。
        var bad = Path.Combine(Path.GetTempPath(), $"idioms-bad-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(bad, """
        {
          "source": "x", "sourceCommit": "y", "license": "MIT",
          "idioms": [
            {"word":"闲花埜草","pinyin":"xián huā yě cǎo","minCharFrequency":3,"tier":1}
          ]
        }
        """);

        try
        {
            var act = async () => await _seeder.SeedAsync(bad);

            (await act.Should().ThrowAsync<InvalidOperationException>())
                .WithMessage("*Tier mismatch*闲花埜草*");
            (await _db.Idioms.AnyAsync()).Should().BeFalse();
        }
        finally
        {
            File.Delete(bad);
        }
    }
}
