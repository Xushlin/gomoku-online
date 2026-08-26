using FluentAssertions;
using Gewu.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gewu.Infrastructure.Tests.Manuals;

/// <summary>
/// **真实产物过真实校验器。**
/// <para>
/// 这个功能的全部依据是「《梅花谱》的 31 条线路、1391 个半手,每一手都能过
/// <c>XiangqiRules</c>」。那件事本来只在应用启动时验一次,而 CI 不启动应用 ——
/// 于是产物里一个字符的手误、或者象棋规则的一次收紧,要等到有人打开学习页才看见。
/// </para>
/// <para>
/// 所以产物跟着测试构建走(见 csproj 的复制规则),这里把它整份灌一遍。
/// </para>
/// </summary>
public class MeihuapuArtefactTests : IAsyncLifetime
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

    /// <summary>产物在测试输出目录里的位置 —— 与运行期同一条相对路径。</summary>
    private static string ArtefactPath =>
        Path.Combine(AppContext.BaseDirectory, XiangqiManualSeeder.MeihuapuPath);

    /// <summary>
    /// 先证明产物真的在那里。**少了这一条,下面那条会在文件缺失时抛 FileNotFound,
    /// 而那个红看起来像「数据坏了」** —— 两件事该分开报。
    /// </summary>
    [Fact]
    public void The_committed_artefact_travels_with_the_build()
    {
        File.Exists(ArtefactPath).Should()
            .BeTrue($"data/manuals/xiangqi-meihuapu.json should be copied to {AppContext.BaseDirectory}");
    }

    [Fact]
    public async Task Every_half_move_in_the_real_manual_is_legal()
    {
        var seeder = new XiangqiManualSeeder(
            "meihuapu", ArtefactPath, _db, NullLogger<XiangqiManualSeeder>.Instance);

        await seeder.SeedAsync();

        var lines = await _db.XiangqiManualLines.AsNoTracking().ToListAsync();
        lines.Should().HaveCount(31, "《梅花谱》前集共 31 条线路");

        var plies = lines.Sum(l => System.Text.Json.JsonDocument.Parse(l.MovesJson)
            .RootElement.GetArrayLength());
        plies.Should().Be(1391);

        // 两级目录的形状:8 局,变化数 6/6/6/5/5/1/1/1。**「恰好」而不是「至少」** ——
        // 数据文件多一条变化时它会红,而那正是该有人看一眼的时刻。
        lines.GroupBy(l => l.Chapter)
            .OrderBy(g => g.Key)
            .Select(g => g.Count())
            .Should().Equal([6, 6, 6, 5, 5, 1, 1, 1]);

        // 谱评两种都在样本里 —— 否则「按评断分色」那类断言会在单一取值上恒真。
        lines.Select(l => l.WinnerSeat).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Seeding_the_real_manual_writes_nothing_outside_its_own_table()
    {
        var seeder = new XiangqiManualSeeder(
            "meihuapu", ArtefactPath, _db, NullLogger<XiangqiManualSeeder>.Instance);

        await seeder.SeedAsync();

        (await _db.Rooms.CountAsync()).Should().Be(0);
        (await _db.Games.CountAsync()).Should().Be(0);
        (await _db.Moves.CountAsync()).Should().Be(0);
        (await _db.UserGameStats.CountAsync()).Should().Be(0);
    }
}
