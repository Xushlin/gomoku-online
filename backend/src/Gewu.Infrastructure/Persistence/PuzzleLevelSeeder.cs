using System.Text.Json;
using System.Text.Json.Serialization;
using Gewu.Domain.Puzzles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gewu.Infrastructure.Persistence;

/// <summary>关卡产物里的一关。<c>Layout</c> / <c>Solution</c> 原样存入,不在此解读。</summary>
public sealed record PuzzleLevelRecord(
    [property: JsonPropertyName("levelIndex")] int LevelIndex,
    [property: JsonPropertyName("difficulty")] int Difficulty,
    [property: JsonPropertyName("layout")] JsonElement Layout,
    [property: JsonPropertyName("solution")] JsonElement Solution);

/// <summary>
/// 关卡产物文件。
/// <para>
/// <c>seed</c> / <c>dictionaryCommit</c> 是**可选**的头部字段:成语纵横的关卡由随机
/// 生成器产出,需要种子与词典 commit 才能复现;华容道的布局是手写的,没有种子可记。
/// 它们因此可空,而不是让第二个游戏编造两个值出来。
/// </para>
/// </summary>
public sealed record PuzzleLevelFile(
    [property: JsonPropertyName("game")] string Game,
    [property: JsonPropertyName("levels")] IReadOnlyList<PuzzleLevelRecord> Levels,
    [property: JsonPropertyName("seed")] int? Seed = null,
    [property: JsonPropertyName("dictionaryCommit")] string? DictionaryCommit = null);

/// <summary>
/// 把提交进仓库的关卡产物灌入 <c>PuzzleLevels</c>。**幂等**:该游戏已有关卡即直接返回。
/// <para>
/// 与 <see cref="IdiomSeeder"/> 同一取舍:关卡数据不进 migration 的 <c>InsertData</c>,
/// 数据库由 migration **加**这份已提交的数据文件共同复现。
/// </para>
/// <para>
/// 幂等性以 <c>(GameKey, LevelIndex)</c> 判定 —— 即 <c>add-puzzle-core</c> 已声明的唯一约束。
/// </para>
/// <para>
/// 游戏键与产物路径是构造参数。此前它叫 <c>CrosswordLevelSeeder</c> 并把这两样写死,
/// 但除了那两个常量之外,它没有任何成语纵横专属的东西 —— 复制一份给华容道会得到
/// 第二份同样的代码。
/// </para>
/// </summary>
public sealed class PuzzleLevelSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppDbContext _db;
    private readonly ILogger<PuzzleLevelSeeder> _logger;

    /// <summary>创建一个针对某个游戏的 seeder。</summary>
    /// <param name="gameKey">游戏键,与产物头部的 <c>game</c> 必须一致。</param>
    /// <param name="relativePath">产物相对输出目录的路径。</param>
    /// <param name="db">数据库上下文。</param>
    /// <param name="logger">日志。</param>
    public PuzzleLevelSeeder(
        string gameKey,
        string relativePath,
        AppDbContext db,
        ILogger<PuzzleLevelSeeder> logger)
    {
        GameKey = gameKey;
        RelativePath = relativePath;
        _db = db;
        _logger = logger;
    }

    /// <summary>本 seeder 负责的游戏键。</summary>
    public string GameKey { get; }

    /// <summary>关卡产物在输出目录中的相对路径。</summary>
    public string RelativePath { get; }

    /// <summary>成语纵横的产物路径。</summary>
    public static string IdiomCrosswordPath => Path.Combine("data", "levels", "idiom-crossword.json");

    /// <summary>华容道的产物路径。</summary>
    public static string KlotskiPath => Path.Combine("data", "levels", "klotski.json");

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
                "Levels for {Game} already present ({Count}); seeder is a no-op.", GameKey, existing);
            return;
        }

        var resolved = path ?? Path.Combine(AppContext.BaseDirectory, RelativePath);
        if (!File.Exists(resolved))
        {
            // 缺产物不是崩溃理由:开发者可能只想跑别的游戏。关卡缺失的表现是那批路由
            // 返回空列表,而不是应用起不来。
            _logger.LogWarning(
                "Level artefact for {Game} not found at {Path}; no levels seeded.", GameKey, resolved);
            return;
        }

        var json = await File.ReadAllTextAsync(resolved, cancellationToken);
        var file = JsonSerializer.Deserialize<PuzzleLevelFile>(json, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Could not parse the level artefact at {resolved}.");

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
            "Seeded {Count} levels for {Game} (seed {Seed}, dictionary {Commit}).",
            file.Levels.Count, GameKey, file.Seed, file.DictionaryCommit);
    }
}
