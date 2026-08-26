using System.Text.Json;
using FluentAssertions;
using Gewu.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gewu.Infrastructure.Tests.Manuals;

/// <summary>
/// 古谱导入。
/// <para>
/// 这里的重点不是「能存进去」,而是**坏数据一定报出来**:校验发生在导入这一次,
/// 所以它是唯一一道关。而它同时是坐标解码的证据 —— 来源的坐标「列在前」,本项目
/// 「行在前」,转置错了的话第一手就会被规则拒掉。
/// </para>
/// </summary>
public class XiangqiManualSeederTests : IAsyncLifetime
{
    private const string Standard = XiangqiManualSeeder.StandardStart;

    /// <summary>真实产物的前两手:相三进五、炮8平5 —— 都在实测过的那 46 手里。</summary>
    private const string TwoLegalPlies = "69477242";

    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private string _path = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        await _db.Database.EnsureCreatedAsync();
        _path = Path.Combine(Path.GetTempPath(), $"manual-{Guid.NewGuid():N}.json");
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private XiangqiManualSeeder Seeder(string path) =>
        new("test", path, _db, NullLogger<XiangqiManualSeeder>.Instance);

    private async Task WriteFileAsync(string startPosition, params (string Title, string Verdict, string Moves)[] lines)
    {
        var payload = new
        {
            startPosition,
            lines = lines.Select(l => new { title = l.Title, verdict = l.Verdict, moves = l.Moves }),
        };
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(payload));
    }

    // ---- 正常路径 ----

    [Fact]
    public async Task Seeds_a_legal_line_and_derives_the_chapter_from_the_title()
    {
        await WriteFileAsync(Standard, ("第3局取中兵压马", "black", TwoLegalPlies));

        await Seeder(_path).SeedAsync();

        var line = await _db.XiangqiManualLines.SingleAsync();
        line.Chapter.Should().Be(3, "局号来自标题,不是另存一列");
        line.OrderInChapter.Should().Be(0);
        line.WinnerSeat.Should().Be(1, "black 映射到后手座位");
        JsonSerializer.Deserialize<int[][]>(line.MovesJson)!.Should().BeEquivalentTo(
            new[] { new[] { 9, 6, 7, 4 }, new[] { 2, 7, 2, 4 } },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Numbers_variations_within_each_chapter_in_file_order()
    {
        await WriteFileAsync(
            Standard,
            ("第1局甲", "red", TwoLegalPlies),
            ("第1局乙", "black", TwoLegalPlies),
            ("第2局丙", "red", TwoLegalPlies));

        await Seeder(_path).SeedAsync();

        var lines = await _db.XiangqiManualLines.OrderBy(l => l.Id).ToListAsync();
        lines.Select(l => (l.Chapter, l.OrderInChapter))
            .Should().Equal([(1, 0), (1, 1), (2, 0)]);
    }

    [Fact]
    public async Task Is_idempotent()
    {
        await WriteFileAsync(Standard, ("第1局甲", "red", TwoLegalPlies));

        await Seeder(_path).SeedAsync();
        await Seeder(_path).SeedAsync();

        (await _db.XiangqiManualLines.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// 一条线路走到最后仍未终局是**常态** —— 实测 31 条里 20 条如此。一条
    /// 「末手必须判定终局」的校验会把它们全部拒掉,而报出来的样子和「数据坏了」一样。
    /// </summary>
    [Fact]
    public async Task Accepts_a_line_that_does_not_end_in_mate()
    {
        await WriteFileAsync(Standard, ("第1局甲", "black", TwoLegalPlies));

        await Seeder(_path).SeedAsync();

        (await _db.XiangqiManualLines.CountAsync()).Should().Be(1);
    }

    // ---- 古谱不许污染对局数据 ----

    /// <summary>
    /// 把古谱塞成 Finished 房间零改动就能复用回放页,而代价是往战绩、ELO、排行榜里
    /// 注入没人下过的棋。这条断言是那个决定唯一会变红的地方。
    /// </summary>
    [Fact]
    public async Task Writes_nothing_outside_its_own_table()
    {
        await WriteFileAsync(
            Standard,
            ("第1局甲", "red", TwoLegalPlies),
            ("第1局乙", "black", TwoLegalPlies));

        await Seeder(_path).SeedAsync();

        (await _db.XiangqiManualLines.CountAsync()).Should().Be(2);
        (await _db.Rooms.CountAsync()).Should().Be(0, "古谱不是对局");
        (await _db.Games.CountAsync()).Should().Be(0);
        (await _db.Moves.CountAsync()).Should().Be(0);
        (await _db.UserGameStats.CountAsync()).Should().Be(0, "古谱不参与评分");
        (await _db.Users.CountAsync()).Should().Be(0);
    }

    // ---- 坏数据 ----

    [Fact]
    public async Task Rejects_an_illegal_half_move_and_names_it()
    {
        // 第 2 手改成一个黑炮走不到的格子。
        await WriteFileAsync(Standard, ("第1局甲", "red", "69477243"));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should()
            .Contain("half-move 2").And.Contain("7243");
        (await _db.XiangqiManualLines.CountAsync()).Should().Be(0, "坏的一条不入库");
    }

    [Fact]
    public async Task Rejects_a_move_string_that_is_not_a_multiple_of_four()
    {
        await WriteFileAsync(Standard, ("第1局甲", "red", "694772"));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("multiple of 4");
    }

    [Fact]
    public async Task Rejects_an_empty_move_string_instead_of_storing_a_zero_move_line()
    {
        await WriteFileAsync(Standard, ("第1局甲", "red", ""));

        var act = async () => await Seeder(_path).SeedAsync();

        await act.Should().ThrowAsync<InvalidDataException>();
        (await _db.XiangqiManualLines.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Rejects_a_non_standard_starting_position()
    {
        await WriteFileAsync("0000" + Standard[4..], ("第1局甲", "red", TwoLegalPlies));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("non-standard position");
    }

    [Fact]
    public async Task Rejects_an_empty_line_list()
    {
        await WriteFileAsync(Standard);

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("no lines");
    }

    [Fact]
    public async Task Rejects_an_unknown_verdict()
    {
        await WriteFileAsync(Standard, ("第1局甲", "draw", TwoLegalPlies));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("verdict");
    }

    [Fact]
    public async Task Rejects_a_title_without_a_chapter_number()
    {
        await WriteFileAsync(Standard, ("取中兵压马", "red", TwoLegalPlies));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("chapter");
    }

    /// <summary>
    /// 缺文件 MUST 抛。量过一次它是 warn:结果是目录端点返回 200 加一个空目录 ——
    /// 一次静默的空导入,而它和成功导入在接口上长得一模一样。
    /// </summary>
    [Fact]
    public async Task Throws_when_the_artefact_is_missing_rather_than_seeding_nothing()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json");

        var act = async () => await Seeder(missing).SeedAsync();

        await act.Should().ThrowAsync<FileNotFoundException>();
    }
}
