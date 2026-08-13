using Gewu.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Tests.Idioms;

/// <summary>
/// <see cref="IdiomRepository"/> 打真 SQLite 的集成测试。
/// <para>
/// 用 in-memory SQLite 而不是 EF 的 InMemory provider:被测的东西恰好是 SQL 层面的行为
/// —— <c>COALESCE(TierOverride, Tier)</c> 过滤、<c>char</c> 列的比较、跨表 join。
/// InMemory provider 会用 LINQ-to-Objects 假装成功,把这些都测空。
/// </para>
/// </summary>
public sealed class IdiomRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private IdiomRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        await _db.Database.EnsureCreatedAsync();

        _db.Idioms.AddRange(
            // tier 1:四字 + 例句 + 出处 + 高字频
            Idiom.FromImport("一举一动", "yī jǔ yī dòng", "每个动作。", "语出《朱子语类》", "他的～都被盯着。", 500),
            Idiom.FromImport("一言九鼎", "yī yán jiǔ dǐng", "说话极有分量。", "语出《史记》", "他～,众人信服。", 300),
            // tier 2:缺例句
            Idiom.FromImport("一丁不识", "yī dīng bù shí", "形容不识字。", "语出《旧唐书》", "无", 100),
            // tier 3:例句出处皆缺
            Idiom.FromImport("闲花埜草", "xián huā yě cǎo", "", "无", "无", 3));
        await _db.SaveChangesAsync();

        _repo = new IdiomRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task FindByWordAsync_returns_the_idiom_regardless_of_tier()
    {
        // 接龙校验不做层级过滤 —— 玩家答一条冷僻但合法的成语,拒掉是 bug。
        (await _repo.FindByWordAsync("闲花埜草"))!.Word.Should().Be("闲花埜草");
    }

    [Fact]
    public async Task FindByWordAsync_returns_null_for_an_unknown_word()
    {
        (await _repo.FindByWordAsync("这不是成语")).Should().BeNull();
    }

    [Fact]
    public async Task FindContainingCharAsync_matches_character_and_position()
    {
        var hits = await _repo.FindContainingCharAsync('举', 1, IdiomTier.Common, 10);

        hits.Should().HaveCount(1);
        hits[0].Word.Should().Be("一举一动");
    }

    [Fact]
    public async Task FindContainingCharAsync_respects_the_position()
    {
        // 「一」在「一举一动」里出现在 0 和 2 两个位置,但不在位置 1。
        (await _repo.FindContainingCharAsync('一', 0, IdiomTier.Common, 10)).Should().NotBeEmpty();
        (await _repo.FindContainingCharAsync('一', 1, IdiomTier.Common, 10)).Should().BeEmpty();
        (await _repo.FindContainingCharAsync('一', 2, IdiomTier.Common, 10)).Should().NotBeEmpty();
    }

    [Fact]
    public async Task FindContainingCharAsync_filters_by_tier()
    {
        // 「一丁不识」是 tier 2,首字也是「一」。
        var common = await _repo.FindContainingCharAsync('一', 0, IdiomTier.Common, 10);
        var usable = await _repo.FindContainingCharAsync('一', 0, IdiomTier.Usable, 10);

        common.Select(i => i.Word).Should().NotContain("一丁不识");
        usable.Select(i => i.Word).Should().Contain("一丁不识");
    }

    [Fact]
    public async Task FindContainingCharAsync_honours_the_limit()
    {
        (await _repo.FindContainingCharAsync('一', 0, IdiomTier.Usable, 1)).Should().HaveCount(1);
    }

    [Fact]
    public async Task FindStartingWithCharAsync_only_matches_position_zero()
    {
        var hits = await _repo.FindStartingWithCharAsync('动', IdiomTier.Obscure, 10);

        // 「动」只出现在「一举一动」的末位,不是首字。
        hits.Should().BeEmpty();
    }

    [Fact]
    public async Task Tier_filter_uses_the_manual_override()
    {
        // 把 tier 3 的条目人工提到 tier 1,它就必须出现在 maxTier=Common 的检索里。
        var obscure = await _db.Idioms.SingleAsync(i => i.Word == "闲花埜草");
        obscure.OverrideTier(IdiomTier.Common);
        await _db.SaveChangesAsync();

        var hits = await _repo.FindContainingCharAsync('闲', 0, IdiomTier.Common, 10);

        hits.Select(i => i.Word).Should().Contain("闲花埜草");
    }

    [Fact]
    public async Task GetRandomAsync_respects_tier_and_count()
    {
        var picks = await _repo.GetRandomAsync(IdiomTier.Common, 2);

        picks.Should().HaveCount(2);
        picks.Should().OnlyContain(i => i.EffectiveTier == IdiomTier.Common);
    }

    [Fact]
    public async Task Deleting_an_idiom_cascades_to_its_characters()
    {
        var idiom = await _db.Idioms.SingleAsync(i => i.Word == "一举一动");
        var before = await _db.IdiomChars.CountAsync();

        _db.Idioms.Remove(idiom);
        await _db.SaveChangesAsync();

        (await _db.IdiomChars.CountAsync()).Should().Be(before - 4);
    }

    [Fact]
    public async Task Word_is_unique()
    {
        _db.Idioms.Add(Idiom.FromImport("一举一动", "dup", "dup", "无", "无", 500));

        var act = async () => await _db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
