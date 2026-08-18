using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Gewu.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gewu.Infrastructure.Tests.Puzzles;

/// <summary>
/// 关卡产物存进数据库时**去掉多余空白**,而语义一字不变。
/// <para>
/// 产物是缩进过提交的 —— 它要被人肉眼审阅。而 <c>JsonElement.GetRawText()</c> 返回源文本
/// 原样的切片,于是那份缩进以前被逐字复制进列里,再在每次加载关卡时发给客户端。
/// 在真实开发库上量过:存下来的字节有 **58% 是空白**。
/// </para>
/// <para>
/// 这一组测试的重点不是「变小了」,而是「变小**并且**一样」—— 一个改了内容的压缩是数据损坏,
/// 而它在体积断言下会显得像成功。所以每一关都用 <c>JsonNode.DeepEquals</c> 对着产物比。
/// </para>
/// <para>
/// 用**缩进过**的产物文本(而不是紧凑的)是这一组的前提:对着一份本来就紧凑的产物测压缩,
/// 断言会全绿而什么都没测到。
/// </para>
/// </summary>
public sealed class PuzzleLevelCompactionTests : IAsyncLifetime
{
    /// <summary>刻意缩进的产物 —— 与仓库里真实产物的形状一致。</summary>
    private const string PrettyArtefact = """
    {
      "game": "klotski",
      "levels": [
        {
          "levelIndex": 0,
          "difficulty": 1,
          "layout": {
            "rows": 5,
            "cols": 4,
            "name": "初识华容",
            "exit": {
              "row": 3,
              "col": 1
            },
            "pieces": [
              {
                "id": "cao",
                "name": "曹操",
                "row": 0,
                "col": 1,
                "height": 2,
                "width": 2,
                "target": true
              },
              {
                "id": "guan",
                "name": "关羽",
                "row": 2,
                "col": 1,
                "height": 1,
                "width": 2
              }
            ]
          },
          "solution": {
            "minMoves": 116
          }
        }
      ]
    }
    """;

    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private string _artefactPath = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        _artefactPath = Path.Combine(Path.GetTempPath(), $"klotski-pretty-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(_artefactPath, PrettyArtefact);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        if (File.Exists(_artefactPath)) File.Delete(_artefactPath);
    }

    private async Task<Domain.Puzzles.PuzzleLevel> SeedOne()
    {
        await new PuzzleLevelSeeder(
                "klotski", PuzzleLevelSeeder.KlotskiPath, _db,
                NullLogger<PuzzleLevelSeeder>.Instance)
            .SeedAsync(_artefactPath);

        return await _db.PuzzleLevels.SingleAsync();
    }

    /// <summary>产物里那一关的 layout / solution 原样文本。</summary>
    private static (string Layout, string Solution) ArtefactRaw()
    {
        using var doc = JsonDocument.Parse(PrettyArtefact);
        var level = doc.RootElement.GetProperty("levels")[0];
        return (level.GetProperty("layout").GetRawText(),
                level.GetProperty("solution").GetRawText());
    }

    [Fact]
    public async Task The_stored_json_says_the_same_thing_as_the_artefact()
    {
        // 这一条比体积断言重要:改了内容的压缩是数据损坏,而它在体积断言下像成功。
        var (rawLayout, rawSolution) = ArtefactRaw();
        var level = await SeedOne();

        JsonNode.DeepEquals(JsonNode.Parse(level.LayoutJson), JsonNode.Parse(rawLayout))
            .Should().BeTrue("压缩只准重排版,不准改语义");
        JsonNode.DeepEquals(JsonNode.Parse(level.SolutionJson), JsonNode.Parse(rawSolution))
            .Should().BeTrue();
    }

    [Fact]
    public async Task The_stored_json_is_smaller_than_the_artefact_text()
    {
        var (rawLayout, _) = ArtefactRaw();
        var level = await SeedOne();

        level.LayoutJson.Length.Should().BeLessThan(
            rawLayout.Length,
            "存的是紧凑形式;这一条红了就说明又在存源文本切片");
    }

    [Fact]
    public async Task The_stored_json_carries_no_insignificant_whitespace()
    {
        var level = await SeedOne();

        OutsideStrings(level.LayoutJson).Should().NotContainAny("\n", "  ");
        OutsideStrings(level.SolutionJson).Should().NotContainAny("\n", "  ");
    }

    [Fact]
    public async Task Chinese_characters_stay_as_characters()
    {
        // 编码器那一脚的陷阱:默认编码器会把每个非 ASCII 字符转义成 \\uXXXX —— 语义相同、
        // DeepEquals 照样绿,但体积**比空白还大**,而且数据库里没法读。所以这一条是独立的。
        var level = await SeedOne();

        level.LayoutJson.Should().Contain("曹操").And.Contain("关羽").And.Contain("初识华容");
        level.LayoutJson.Should().NotContain("\\u");
    }

    [Fact]
    public async Task Compaction_beats_escaping_on_size()
    {
        // 上一条的量化版:如果有人把编码器换回默认值,DeepEquals 与「无空白」都还是绿的,
        // 而这一条会红 —— 转义之后的文本比缩进过的原文还长。
        var (rawLayout, _) = ArtefactRaw();
        var level = await SeedOne();
        var escapedLength = JsonSerializer.Serialize(JsonNode.Parse(rawLayout)).Length;

        level.LayoutJson.Length.Should().BeLessThan(escapedLength);
    }

    /// <summary>把字符串字面量挖掉,只留结构部分 —— 中文与空格本来就可以出现在值里。</summary>
    private static string OutsideStrings(string json)
    {
        var outside = new System.Text.StringBuilder(json.Length);
        var inString = false;
        var escaped = false;
        foreach (var ch in json)
        {
            if (inString)
            {
                if (escaped) escaped = false;
                else if (ch == '\\') escaped = true;
                else if (ch == '"') inString = false;
                continue;
            }
            if (ch == '"') { inString = true; continue; }
            outside.Append(ch);
        }
        return outside.ToString();
    }
}
