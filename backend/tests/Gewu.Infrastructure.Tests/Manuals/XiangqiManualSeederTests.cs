using System.Text.Json;
using FluentAssertions;
using Gewu.Domain.Manuals;
using Gewu.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gewu.Infrastructure.Tests.Manuals;

/// <summary>
/// 古谱导入。
/// <para>
/// 重点不是「能存进去」,而是**坏数据一定报出来**,以及**两条校验路径各自守住自己那条线**:
/// 标准开局逐手过规则,残局只做结构校验 —— 而后者明确更弱,所以它能查的和查不了的都要
/// 有断言钉着。
/// </para>
/// </summary>
public class XiangqiManualSeederTests : IAsyncLifetime
{
    private const string Standard = XiangqiManualSeeder.StandardBoard;

    /// <summary>真实产物的前两手:相三进五、炮8平5 —— 都在实测过的那 46 手里。</summary>
    private const string TwoLegalPlies = "69477242";

    /// <summary>一个残局:红帅 (9,4)、红车 (9,0);黑将 (0,4)、黑卒 (3,4)。</summary>
    private static readonly string Endgame = Board(
        ("k", 0, 4), ("p", 3, 4), ("K", 9, 4), ("R", 9, 0));

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
        if (File.Exists(_path)) File.Delete(_path);
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    /// <summary>拼一个盘面串 —— 测试里手写 90 个点是不可读的。</summary>
    private static string Board(params (string Piece, int Row, int Col)[] pieces)
    {
        var b = new char[XiangqiManualLine.BoardStringLength];
        Array.Fill(b, '.');
        foreach (var (piece, row, col) in pieces) b[row * 9 + col] = piece[0];
        return new string(b);
    }

    private XiangqiManualSeeder Seeder(string path) =>
        new(path, _db, NullLogger<XiangqiManualSeeder>.Instance);

    private async Task WriteAsync(
        bool grouped,
        params (string Title, string Verdict, int FirstSeat, string Start, string Moves)[] lines)
    {
        var payload = new
        {
            manual = new { key = "test", name = "测试谱", grouped },
            lines = lines.Select(l => new
            {
                title = l.Title, verdict = l.Verdict, firstSeat = l.FirstSeat,
                start = l.Start, moves = l.Moves,
            }),
        };
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(payload));
    }

    // ---- 标准开局那条路径 ----

    [Fact]
    public async Task Seeds_a_standard_opening_line_and_derives_the_chapter_from_the_title()
    {
        await WriteAsync(true, ("第3局取中兵压马", "black", 0, Standard, TwoLegalPlies));

        await Seeder(_path).SeedAsync();

        var line = await _db.XiangqiManualLines.SingleAsync();
        line.Chapter.Should().Be(3, "局号来自标题,不是另存一列");
        line.Verdict.Should().Be(ManualVerdict.BlackBetter);
        line.StartPosition.Should().Be(Standard);
        line.FirstSeat.Should().Be(0);
        JsonSerializer.Deserialize<int[][]>(line.MovesJson)!.Should().BeEquivalentTo(
            new[] { new[] { 9, 6, 7, 4 }, new[] { 2, 7, 2, 4 } },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Rejects_an_illegal_half_move_on_the_standard_path()
    {
        await WriteAsync(true, ("第1局甲", "red", 0, Standard, "69477243"));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("half-move 2").And.Contain("rejected");
        (await _db.XiangqiManualLines.CountAsync()).Should().Be(0);
    }

    /// <summary>
    /// 走到最后仍未终局是**常态** —— 《梅花谱》31 条里 20 条如此。一条「末手必须终局」的
    /// 校验会把它们全部拒掉,而报出来的样子和「数据坏了」一模一样。
    /// </summary>
    [Fact]
    public async Task Accepts_a_standard_line_that_does_not_end_in_mate()
    {
        await WriteAsync(true, ("第1局甲", "black", 0, Standard, TwoLegalPlies));

        await Seeder(_path).SeedAsync();

        (await _db.XiangqiManualLines.CountAsync()).Should().Be(1);
    }

    // ---- 残局那条路径 ----

    /// <summary>红车横移到 (9,4)?不行,那格有帅。这里让车走到 (8,0):只搬子,不判合法性。</summary>
    [Fact]
    public async Task Seeds_an_endgame_through_the_structural_path()
    {
        // 红车 (9,0) -> (8,0) 是 "0908";黑卒 (3,4) -> (4,4) 是 "4344"。
        await WriteAsync(false, ("车马和卒001", "draw", 0, Endgame, "09084344"));

        await Seeder(_path).SeedAsync();

        var line = await _db.XiangqiManualLines.SingleAsync();
        line.Chapter.Should().Be(0, "没有分组层的谱不许编局号");
        line.Verdict.Should().Be(ManualVerdict.Draw);
        line.StartPosition.Should().Be(Endgame);
    }

    /// <summary>
    /// **残局不走规则那条路。** 这里的第一手在标准开局下是非法的(那格没有子),
    /// 而它在这个残局里完全正常 —— 若实现把残局也交给规则重放,这条会红。
    /// </summary>
    [Fact]
    public async Task Does_not_replay_an_endgame_through_the_rules()
    {
        await WriteAsync(false, ("车和卒002", "draw", 0, Endgame, "0908"));

        await Seeder(_path).SeedAsync();

        (await _db.XiangqiManualLines.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Rejects_an_endgame_move_that_starts_from_an_empty_square()
    {
        // (5,5) 上没有子。
        await WriteAsync(false, ("车和卒003", "draw", 0, Endgame, "5545"));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("half-move 1").And.Contain("empty square");
    }

    /// <summary>
    /// **黑先走的题不是坏数据** —— 1634 局里 7 局如此。所以起点是存下来的先走方。
    /// </summary>
    [Fact]
    public async Task Accepts_a_line_where_black_moves_first()
    {
        // 黑卒 (3,4) -> (4,4) 先走,然后红车 (9,0) -> (8,0)。
        await WriteAsync(false, ("黑先和001", "draw", 1, Endgame, "43440908"));

        await Seeder(_path).SeedAsync();

        var line = await _db.XiangqiManualLines.SingleAsync();
        line.FirstSeat.Should().Be(1);
    }

    /// <summary>而**中途换手**才是坏数据 —— 交替是结构校验唯一能抓到它的地方。</summary>
    [Fact]
    public async Task Rejects_a_line_where_one_side_moves_twice()
    {
        // 红车走两手:(9,0)->(8,0) 然后 (8,0)->(7,0)。
        await WriteAsync(false, ("连走001", "draw", 0, Endgame, "09080807"));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("half-move 2").And.Contain("alternation");
    }

    // ---- 谱评四态 ----

    [Theory]
    [InlineData("red", ManualVerdict.RedBetter)]
    [InlineData("black", ManualVerdict.BlackBetter)]
    [InlineData("draw", ManualVerdict.Draw)]
    [InlineData("unrecorded", ManualVerdict.Unrecorded)]
    public async Task Maps_every_verdict_including_draw_and_unrecorded(string raw, ManualVerdict expected)
    {
        await WriteAsync(false, ("甲001", raw, 0, Endgame, "0908"));

        await Seeder(_path).SeedAsync();

        (await _db.XiangqiManualLines.SingleAsync()).Verdict.Should().Be(expected);
    }

    [Fact]
    public async Task Rejects_an_unknown_verdict_rather_than_defaulting_to_a_side()
    {
        await WriteAsync(false, ("甲001", "probably-red", 0, Endgame, "0908"));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("verdict");
    }

    // ---- 形状 ----

    [Fact]
    public async Task Rejects_a_start_position_of_the_wrong_length()
    {
        await WriteAsync(false, ("甲001", "draw", 0, Endgame[..89], "0908"));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("89");
    }

    [Fact]
    public async Task Rejects_a_move_string_that_is_not_a_multiple_of_four()
    {
        await WriteAsync(false, ("甲001", "draw", 0, Endgame, "090"));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("multiple of 4");
    }

    [Fact]
    public async Task Rejects_an_empty_move_string_instead_of_storing_a_zero_move_line()
    {
        await WriteAsync(false, ("甲001", "draw", 0, Endgame, ""));

        var act = async () => await Seeder(_path).SeedAsync();

        await act.Should().ThrowAsync<InvalidDataException>();
        (await _db.XiangqiManualLines.CountAsync()).Should().Be(0);
    }

    /// <summary>只有一手的线路 MUST 入库 —— 实测 77 局如此,一个手数下限会静静吃掉它们。</summary>
    [Fact]
    public async Task Accepts_a_single_half_move_line()
    {
        await WriteAsync(false, ("一手001", "red", 0, Endgame, "0908"));

        await Seeder(_path).SeedAsync();

        var line = await _db.XiangqiManualLines.SingleAsync();
        JsonSerializer.Deserialize<int[][]>(line.MovesJson)!.Should().HaveCount(1);
    }

    [Fact]
    public async Task Rejects_an_empty_line_list()
    {
        await WriteAsync(false);

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("no lines");
    }

    [Fact]
    public async Task Rejects_a_grouped_title_without_a_chapter_number()
    {
        await WriteAsync(true, ("取中兵压马", "red", 0, Standard, TwoLegalPlies));

        var act = async () => await Seeder(_path).SeedAsync();

        (await act.Should().ThrowAsync<InvalidDataException>())
            .Which.Message.Should().Contain("chapter");
    }

    // ---- 谱的身份、幂等、不碰对局数据 ----

    [Fact]
    public async Task Records_the_manual_identity_from_the_file()
    {
        await WriteAsync(false, ("甲001", "draw", 0, Endgame, "0908"));

        await Seeder(_path).SeedAsync();

        var manual = await _db.XiangqiManuals.SingleAsync();
        manual.Key.Should().Be("test");
        manual.Name.Should().Be("测试谱");
        manual.Grouped.Should().BeFalse();
    }

    [Fact]
    public async Task Is_idempotent()
    {
        await WriteAsync(false, ("甲001", "draw", 0, Endgame, "0908"));

        await Seeder(_path).SeedAsync();
        await Seeder(_path).SeedAsync();

        (await _db.XiangqiManualLines.CountAsync()).Should().Be(1);
        (await _db.XiangqiManuals.CountAsync()).Should().Be(1);
    }

    /// <summary>
    /// 把古谱塞成 Finished 房间零改动就能复用回放页,而代价是往战绩、ELO、排行榜里注入
    /// 没人下过的棋。这条断言是那个决定唯一会变红的地方 —— 多了六辑也不能丢。
    /// </summary>
    [Fact]
    public async Task Writes_nothing_outside_its_own_tables()
    {
        await WriteAsync(false,
            ("甲001", "draw", 0, Endgame, "0908"),
            ("乙002", "red", 0, Endgame, "0908"));

        await Seeder(_path).SeedAsync();

        (await _db.XiangqiManualLines.CountAsync()).Should().Be(2);
        (await _db.Rooms.CountAsync()).Should().Be(0, "古谱不是对局");
        (await _db.Games.CountAsync()).Should().Be(0);
        (await _db.Moves.CountAsync()).Should().Be(0);
        (await _db.UserGameStats.CountAsync()).Should().Be(0, "古谱不参与评分");
        (await _db.Users.CountAsync()).Should().Be(0);
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
