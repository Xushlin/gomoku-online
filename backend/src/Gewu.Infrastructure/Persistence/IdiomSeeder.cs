using System.Text.Json;
using System.Text.Json.Serialization;
using Gewu.Domain.Idioms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gewu.Infrastructure.Persistence;

/// <summary>精选产物里的一条成语。<c>Tier</c> 是产物记录的层级,种子载入时会被重算校验。</summary>
public sealed record CuratedIdiom(
    [property: JsonPropertyName("word")] string Word,
    [property: JsonPropertyName("pinyin")] string Pinyin,
    [property: JsonPropertyName("minCharFrequency")] int MinCharFrequency,
    [property: JsonPropertyName("tier")] int Tier,
    [property: JsonPropertyName("explanation")] string? Explanation,
    [property: JsonPropertyName("derivation")] string? Derivation,
    [property: JsonPropertyName("example")] string? Example);

/// <summary>精选产物的整体形状,头部字段记录来源以保证可追溯。</summary>
public sealed record CuratedIdiomFile(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("sourceCommit")] string SourceCommit,
    [property: JsonPropertyName("license")] string License,
    [property: JsonPropertyName("idioms")] IReadOnlyList<CuratedIdiom> Idioms);

/// <summary>
/// 把提交进仓库的成语精选产物灌入数据库。**幂等**:表非空即直接返回。
/// <para>
/// 幂等性以 <c>Word</c> 判定而非行 Id —— 产物是可重新生成的,自增 Id 不稳定。
/// </para>
/// <para>
/// 30,895 条不放进 migration 的 <c>InsertData</c>:那样的 migration 文件没人 review 得动。
/// 代价是数据库不再"仅凭 migration 就能完全重建",而是需要 migration 加这份提交进仓库的
/// 数据文件 —— 这是 design 里明确接受的权衡。
/// </para>
/// </summary>
public sealed class IdiomSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly AppDbContext _db;
    private readonly ILogger<IdiomSeeder> _logger;

    /// <inheritdoc />
    public IdiomSeeder(AppDbContext db, ILogger<IdiomSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>精选产物在输出目录中的相对路径。</summary>
    public static string DefaultRelativePath => Path.Combine("data", "idioms.curated.json");

    /// <summary>
    /// 若 <c>Idioms</c> 表为空则载入产物,否则无操作。
    /// </summary>
    /// <param name="path">产物路径;传 <c>null</c> 时取输出目录下的默认路径。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次实际写入的成语条数;已有数据时为 0。</returns>
    /// <exception cref="InvalidOperationException">
    /// 产物记录的层级与 <see cref="IdiomTiering.Classify"/> 重算结果不一致 —— 说明产物
    /// 是用另一套阈值生成的,需要重跑导入器,而不是让两份真源悄悄分叉。
    /// </exception>
    public async Task<int> SeedAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        if (await _db.Idioms.AnyAsync(cancellationToken))
        {
            return 0;
        }

        var resolved = path ?? Path.Combine(AppContext.BaseDirectory, DefaultRelativePath);
        if (!File.Exists(resolved))
        {
            _logger.LogWarning(
                "Idiom artefact not found at {Path}; dictionary stays empty. Run tools/IdiomImporter to produce it.",
                resolved);
            return 0;
        }

        await using var stream = File.OpenRead(resolved);
        var file = await JsonSerializer.DeserializeAsync<CuratedIdiomFile>(
            stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Idiom artefact at {resolved} deserialised to null.");

        var entities = new List<Idiom>(file.Idioms.Count);
        foreach (var row in file.Idioms)
        {
            var idiom = Idiom.FromImport(
                row.Word, row.Pinyin, row.Explanation, row.Derivation, row.Example, row.MinCharFrequency);

            if ((int)idiom.Tier != row.Tier)
            {
                throw new InvalidOperationException(
                    $"Tier mismatch for '{row.Word}': artefact says {row.Tier}, " +
                    $"IdiomTiering.Classify says {(int)idiom.Tier}. The artefact was generated with " +
                    "different thresholds — re-run tools/IdiomImporter.");
            }

            entities.Add(idiom);
        }

        _db.Idioms.AddRange(entities);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded {Count} idioms from {Source}@{Commit}.",
            entities.Count, file.Source, file.SourceCommit);

        return entities.Count;
    }
}
