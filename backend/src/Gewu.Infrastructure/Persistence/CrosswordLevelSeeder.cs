using System.Text.Json;
using System.Text.Json.Serialization;
using Gewu.Domain.Puzzles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gewu.Infrastructure.Persistence;

/// <summary>关卡产物里的一关。<c>Layout</c> / <c>Solution</c> 原样存入,不在此解读。</summary>
public sealed record CrosswordLevelRecord(
    [property: JsonPropertyName("levelIndex")] int LevelIndex,
    [property: JsonPropertyName("difficulty")] int Difficulty,
    [property: JsonPropertyName("layout")] JsonElement Layout,
    [property: JsonPropertyName("solution")] JsonElement Solution);

/// <summary>关卡产物文件。头部字段记录种子与词典 commit,让任意一关都可追溯、可复现。</summary>
public sealed record CrosswordLevelFile(
    [property: JsonPropertyName("game")] string Game,
    [property: JsonPropertyName("seed")] int Seed,
    [property: JsonPropertyName("dictionaryCommit")] string DictionaryCommit,
    [property: JsonPropertyName("levels")] IReadOnlyList<CrosswordLevelRecord> Levels);

/// <summary>
/// 把提交进仓库的成语纵横关卡灌入 <c>PuzzleLevels</c>。**幂等**:该游戏已有关卡即直接返回。
/// <para>
/// 与 <see cref="IdiomSeeder"/> 同一取舍:关卡数据不进 migration 的 <c>InsertData</c>,
/// 数据库由 migration **加**这份已提交的数据文件共同复现。
/// </para>
/// <para>
/// 幂等性以 <c>(GameKey, LevelIndex)</c> 判定 —— 即 <c>add-puzzle-core</c> 已声明的唯一约束。
/// </para>
/// </summary>
public sealed class CrosswordLevelSeeder
{
    /// <summary>本 seeder 负责的游戏键。</summary>
    public const string GameKey = "idiom-crossword";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppDbContext _db;
    private readonly ILogger<CrosswordLevelSeeder> _logger;

    /// <inheritdoc />
    public CrosswordLevelSeeder(AppDbContext db, ILogger<CrosswordLevelSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>关卡产物在输出目录中的相对路径。</summary>
    public static string DefaultRelativePath
        => Path.Combine("data", "levels", "idiom-crossword.json");

    /// <summary>
    /// 若本游戏尚无关卡则载入产物,否则无操作。
    /// </summary>
    /// <param name="path">产物路径;传 <c>null</c> 时取输出目录下的默认路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task SeedAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        var existing = await _db.PuzzleLevels
            .CountAsync(l => l.GameKey == GameKey, cancellationToken);
        if (existing > 0)
        {
            _logger.LogDebug(
                "Crossword levels already present ({Count}); seeder is a no-op.", existing);
            return;
        }

        var resolved = path ?? Path.Combine(AppContext.BaseDirectory, DefaultRelativePath);
        if (!File.Exists(resolved))
        {
            // 缺产物不是崩溃理由:开发者可能只想跑别的游戏。关卡缺失的表现是那批路由
            // 返回空列表,而不是应用起不来。
            _logger.LogWarning(
                "Crossword level artefact not found at {Path}; no levels seeded.", resolved);
            return;
        }

        var json = await File.ReadAllTextAsync(resolved, cancellationToken);
        var file = JsonSerializer.Deserialize<CrosswordLevelFile>(json, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Could not parse the crossword level artefact at {resolved}.");

        if (file.Game != GameKey)
        {
            throw new InvalidOperationException(
                $"Level artefact declares game '{file.Game}' but this seeder handles '{GameKey}'.");
        }

        foreach (var record in file.Levels.OrderBy(l => l.LevelIndex))
        {
            _db.PuzzleLevels.Add(PuzzleLevel.Create(
                GameKey,
                record.LevelIndex,
                record.Difficulty,
                record.Layout.GetRawText(),
                record.Solution.GetRawText()));
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded {Count} crossword levels (seed {Seed}, dictionary {Commit}).",
            file.Levels.Count, file.Seed, file.DictionaryCommit);
    }
}
