using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gewu.Domain.Games.IdiomCrossword;

namespace Gewu.Tools.CrosswordGenerator;

/// <summary>精选产物里的一条成语(与 <c>IdiomSeeder</c> 的 <c>CuratedIdiom</c> 同构)。</summary>
internal sealed record CuratedIdiom(
    [property: JsonPropertyName("word")] string Word,
    [property: JsonPropertyName("tier")] int Tier,
    [property: JsonPropertyName("explanation")] string? Explanation);

/// <summary>精选产物文件。</summary>
internal sealed record CuratedFile(
    [property: JsonPropertyName("sourceCommit")] string SourceCommit,
    [property: JsonPropertyName("idioms")] IReadOnlyList<CuratedIdiom> Idioms);

/// <summary>产物中的一个关卡。</summary>
internal sealed record LevelRecord(
    [property: JsonPropertyName("levelIndex")] int LevelIndex,
    [property: JsonPropertyName("difficulty")] int Difficulty,
    [property: JsonPropertyName("layout")] CrosswordLayout Layout,
    [property: JsonPropertyName("solution")] CrosswordSolution Solution);

/// <summary>关卡产物文件 —— 头部记录来源,让任意一关都可追溯、可复现。</summary>
internal sealed record LevelFile(
    [property: JsonPropertyName("game")] string Game,
    [property: JsonPropertyName("seed")] int Seed,
    [property: JsonPropertyName("dictionaryCommit")] string DictionaryCommit,
    [property: JsonPropertyName("generatedAt")] string GeneratedAt,
    [property: JsonPropertyName("maxTier")] int MaxTier,
    [property: JsonPropertyName("dials")] IReadOnlyList<string> Dials,
    [property: JsonPropertyName("levels")] IReadOnlyList<LevelRecord> Levels);

/// <summary>
/// 把 <c>backend/data/idioms.curated.json</c> 转成 <c>backend/data/levels/idiom-crossword.json</c>。
///
/// 用法:
///   dotnet run --project backend/tools/CrosswordGenerator -- &lt;curated.json&gt; &lt;output.json&gt; &lt;seed&gt; [generatedAt]
///
/// 本工具只负责 **I/O 与配置**:读语料、跑难度阶梯、写产物。摆放规则、相邻不变式与审计
/// 都在 <c>Gewu.Domain.Games.IdiomCrossword</c> 里,和 <c>IdiomCrosswordRules</c> 同处一地并
/// 被单元测试覆盖 —— 与 <c>IdiomImporter</c> 把分层交给 <c>IdiomTiering</c> 是同一条纪律:
/// 工具绝不持有规则的第二份副本。
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // 中文不转义,产物才读得懂、diff 才有意义。
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>难度阶梯 —— 三个旋钮都显式写在这里,调曲线是改配置 + 重新生成,不是改算法。</summary>
    private static readonly DifficultyDial[] Ladder =
    {
        new(IdiomCount: 2, GivenCount: 2, DistractorCount: 0),
        new(IdiomCount: 3, GivenCount: 2, DistractorCount: 0),
        new(IdiomCount: 4, GivenCount: 2, DistractorCount: 0),
        new(IdiomCount: 5, GivenCount: 2, DistractorCount: 2),
        new(IdiomCount: 6, GivenCount: 2, DistractorCount: 3),
        new(IdiomCount: 7, GivenCount: 3, DistractorCount: 4),
        new(IdiomCount: 8, GivenCount: 3, DistractorCount: 5),
        new(IdiomCount: 9, GivenCount: 3, DistractorCount: 6),
        new(IdiomCount: 10, GivenCount: 3, DistractorCount: 6),
        new(IdiomCount: 11, GivenCount: 4, DistractorCount: 7),
        new(IdiomCount: 12, GivenCount: 4, DistractorCount: 8),
        new(IdiomCount: 12, GivenCount: 3, DistractorCount: 10),
    };

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length is < 3 or > 4)
        {
            Console.Error.WriteLine(
                "usage: CrosswordGenerator <curated.json> <output.json> <seed> [generatedAt]");
            return 1;
        }

        var curatedPath = args[0];
        var outputPath = args[1];
        if (!int.TryParse(args[2], out var seed))
        {
            Console.Error.WriteLine($"seed must be an integer, got '{args[2]}'.");
            return 1;
        }
        var generatedAt = args.Length == 4 ? args[3] : DateTime.UtcNow.ToString("O");

        if (!File.Exists(curatedPath))
        {
            Console.Error.WriteLine($"curated file not found: {curatedPath}");
            return 1;
        }

        var curated = JsonSerializer.Deserialize<CuratedFile>(
            File.ReadAllText(curatedPath), ReadOptions);
        if (curated is null)
        {
            Console.Error.WriteLine("could not parse the curated file.");
            return 1;
        }

        // 只要 tier 1 的四字成语,且必须有释义 —— 答对之后要能拿出那张"纸条"。
        var corpus = curated.Idioms
            .Where(i => i.Tier == 1
                && i.Word.Length == 4
                && !string.IsNullOrWhiteSpace(i.Explanation))
            .Select(i => new SourceIdiom(i.Word, i.Explanation!))
            .ToList();

        Console.WriteLine($"corpus: {corpus.Count} tier-1 four-character idioms with explanations");
        if (corpus.Count < 100)
        {
            Console.Error.WriteLine("corpus is too small to generate a level set.");
            return 1;
        }

        var dictionary = corpus.Select(i => i.Word).ToHashSet(StringComparer.Ordinal);
        var generator = new CrosswordLevelGenerator(corpus, seed);

        var levels = new List<LevelRecord>();
        var rejected = 0;

        for (var i = 0; i < Ladder.Length; i++)
        {
            var dial = Ladder[i];
            var level = generator.Generate(dial, difficulty: i + 1);
            var audit = CrosswordAudit.Check(level, dictionary);

            if (!audit.Passed)
            {
                rejected++;
                Console.Error.WriteLine($"level {i} REJECTED:");
                foreach (var failure in audit.Failures)
                {
                    Console.Error.WriteLine($"  - {failure}");
                }
                continue;
            }

            levels.Add(new LevelRecord(
                LevelIndex: levels.Count,
                Difficulty: level.Difficulty,
                Layout: level.Layout,
                Solution: level.Solution));

            Console.WriteLine(
                $"level {levels.Count - 1}: {level.Layout.Slots.Count} idioms, "
                + $"{level.Layout.Rows}×{level.Layout.Cols} grid, "
                + $"{level.Layout.Given.Count} given, {level.Layout.Tray.Count} tiles");
        }

        if (levels.Count == 0)
        {
            Console.Error.WriteLine("no level passed audit; nothing written.");
            return 1;
        }

        var file = new LevelFile(
            Game: "idiom-crossword",
            Seed: seed,
            DictionaryCommit: curated.SourceCommit,
            GeneratedAt: generatedAt,
            MaxTier: 1,
            Dials: Ladder.Select(d =>
                $"idioms={d.IdiomCount},given={d.GivenCount},distractors={d.DistractorCount}").ToList(),
            Levels: levels);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(file, WriteOptions) + "\n");

        Console.WriteLine();
        Console.WriteLine($"wrote {levels.Count} levels to {outputPath} ({rejected} rejected)");
        return 0;
    }
}
