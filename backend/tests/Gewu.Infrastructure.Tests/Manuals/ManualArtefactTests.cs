using System.Text.Json;
using FluentAssertions;
using Gewu.Domain.Manuals;
using Gewu.Infrastructure;
using Gewu.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gewu.Infrastructure.Tests.Manuals;

/// <summary>
/// **真实产物过真实校验器 —— 七部谱,一部不落。**
/// <para>
/// 这件事本来只在应用启动时验一次,而 CI 不启动应用 —— 于是产物里一个字符的手误、或者
/// 象棋规则的一次收紧,要等到有人打开学习页才看见。所以产物跟着测试构建走(见 csproj 的
/// 复制规则),这里把每一部整份灌一遍。
/// </para>
/// <para>
/// **清单从 <see cref="DependencyInjection.XiangqiManualKeys"/> 推导**,不是手写 ——
/// 一份手写名单会在加第八辑那天静静落后,而这个仓库为这个形状修过七次。
/// </para>
/// </summary>
public class ManualArtefactTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        await _db.Database.EnsureCreatedAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static string PathFor(string key) =>
        Path.Combine(AppContext.BaseDirectory, XiangqiManualSeeder.PathFor(key));

    /// <summary>
    /// 先证明产物真的在那里。**少了这一条,下面那条会在文件缺失时抛 FileNotFound,
    /// 而那个红看起来像「数据坏了」** —— 两件事该分开报。
    /// </summary>
    [Fact]
    public void Every_registered_manual_travels_with_the_build()
    {
        DependencyInjection.XiangqiManualKeys.Should().NotBeEmpty();
        var missing = DependencyInjection.XiangqiManualKeys
            .Where(k => !File.Exists(PathFor(k)))
            .ToList();
        missing.Should().BeEmpty("every registered manual needs its artefact copied to the output");
    }

    [Fact]
    public async Task Every_line_of_every_manual_passes_its_own_validation_path()
    {
        foreach (var key in DependencyInjection.XiangqiManualKeys)
        {
            var seeder = new XiangqiManualSeeder(
                PathFor(key), _db, NullLogger<XiangqiManualSeeder>.Instance);
            await seeder.SeedAsync();
        }

        var lines = await _db.XiangqiManualLines.AsNoTracking().ToListAsync();
        var manuals = await _db.XiangqiManuals.AsNoTracking().ToListAsync();

        manuals.Should().HaveCount(DependencyInjection.XiangqiManualKeys.Count);
        // 《梅花谱》31 + 六辑残局 1634。**「恰好」而不是「至少」** —— 数据文件多一条时它会红,
        // 而那正是该有人看一眼的时刻。
        lines.Should().HaveCount(1665);

        var plies = lines.Sum(l => JsonDocument.Parse(l.MovesJson).RootElement.GetArrayLength());
        plies.Should().Be(30508, "1391(梅花谱) + 29117(六辑残局)");
    }

    /// <summary>
    /// **两条校验路径都要在样本里** —— 否则「按起始局面分路径」在单一类别上恒真。
    /// 实测:标准开局 188(梅花谱 31 + 残局里的让子谱 157),非标准 1477。
    /// </summary>
    [Fact]
    public async Task Both_validation_paths_are_exercised_by_the_real_data()
    {
        foreach (var key in DependencyInjection.XiangqiManualKeys)
        {
            await new XiangqiManualSeeder(
                PathFor(key), _db, NullLogger<XiangqiManualSeeder>.Instance).SeedAsync();
        }

        var lines = await _db.XiangqiManualLines.AsNoTracking().ToListAsync();
        var standard = lines.Count(l => l.StartPosition == XiangqiManualSeeder.StandardBoard);
        standard.Should().Be(188);
        (lines.Count - standard).Should().Be(1477);
    }

    /// <summary>四种谱评、两种先走方、残局与满盘 —— 每一类都要真的出现过。</summary>
    [Fact]
    public async Task Every_verdict_first_seat_and_board_size_appears_in_the_real_data()
    {
        foreach (var key in DependencyInjection.XiangqiManualKeys)
        {
            await new XiangqiManualSeeder(
                PathFor(key), _db, NullLogger<XiangqiManualSeeder>.Instance).SeedAsync();
        }

        var lines = await _db.XiangqiManualLines.AsNoTracking().ToListAsync();

        lines.Select(l => l.Verdict).Distinct().Should().BeEquivalentTo(
            new[]
            {
                ManualVerdict.RedBetter, ManualVerdict.BlackBetter,
                ManualVerdict.Draw, ManualVerdict.Unrecorded,
            },
            "四态都要在样本里,否则「按谱评显示」在更少的取值上恒真");

        // 黑先走 7 局 —— 「恰好」,因为第 8 局出现时该有人来看一眼。
        lines.Count(l => l.FirstSeat == 1).Should().Be(7);
        lines.Count(l => l.FirstSeat == 0).Should().Be(1658);

        var pieces = lines.Select(l => l.StartPosition.Count(c => c != '.')).ToList();
        pieces.Min().Should().Be(4);
        pieces.Max().Should().Be(32);
        // 满盘 163 ≠ 标准开局 157:**有 6 局是 32 子却不是标准摆法**,所以子数 MUST NOT
        // 被当成「是不是标准开局」的判据。
        pieces.Count(p => p == 32).Should().Be(163 + 31, "六辑的 163 加《梅花谱》的 31");
    }

    [Fact]
    public async Task Seeding_every_manual_writes_nothing_outside_its_own_tables()
    {
        foreach (var key in DependencyInjection.XiangqiManualKeys)
        {
            await new XiangqiManualSeeder(
                PathFor(key), _db, NullLogger<XiangqiManualSeeder>.Instance).SeedAsync();
        }

        (await _db.Rooms.CountAsync()).Should().Be(0);
        (await _db.Games.CountAsync()).Should().Be(0);
        (await _db.Moves.CountAsync()).Should().Be(0);
        (await _db.UserGameStats.CountAsync()).Should().Be(0);
    }
}
