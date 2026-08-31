using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gewu.Domain.Games.IdiomGuess;

namespace Gewu.Tools.IdiomGuessGenerator;

/// <summary>精选产物里的一条成语(与 <c>IdiomSeeder</c> 的 <c>CuratedIdiom</c> 同构)。</summary>
internal sealed record CuratedIdiom(
    [property: JsonPropertyName("word")] string Word,
    [property: JsonPropertyName("tier")] int Tier,
    [property: JsonPropertyName("explanation")] string? Explanation,
    [property: JsonPropertyName("derivation")] string? Derivation);

/// <summary>精选产物文件。</summary>
internal sealed record CuratedFile(
    [property: JsonPropertyName("sourceCommit")] string SourceCommit,
    [property: JsonPropertyName("idioms")] IReadOnlyList<CuratedIdiom> Idioms);

/// <summary>产物中的一个关卡。</summary>
internal sealed record LevelRecord(
    [property: JsonPropertyName("levelIndex")] int LevelIndex,
    [property: JsonPropertyName("difficulty")] int Difficulty,
    [property: JsonPropertyName("layout")] IdiomGuessLayout Layout,
    [property: JsonPropertyName("solution")] IdiomGuessSolution Solution);

/// <summary>关卡产物文件 —— 头部记录来源,让任意一关都可追溯、可复现。</summary>
internal sealed record LevelFile(
    [property: JsonPropertyName("game")] string Game,
    [property: JsonPropertyName("seed")] int Seed,
    [property: JsonPropertyName("dictionaryCommit")] string DictionaryCommit,
    [property: JsonPropertyName("generatedAt")] string GeneratedAt,
    [property: JsonPropertyName("dials")] IReadOnlyList<string> Dials,
    [property: JsonPropertyName("levels")] IReadOnlyList<LevelRecord> Levels);

/// <summary>
/// 把 <c>backend/data/idioms.curated.json</c> 转成 <c>backend/data/levels/idiom-guess.json</c>。
///
/// 用法:
///   dotnet run --project backend/tools/IdiomGuessGenerator -- &lt;curated.json&gt; &lt;output.json&gt; &lt;seed&gt; [generatedAt]
///
/// 本工具只负责 **I/O 与配置**。那条真正要紧的规则 —— 被挖的字不得出现在自己的释义里 ——
/// 在 <c>IdiomGuessLevelGenerator.BlankablePositions</c> 里,和规则同处一地并被单元测试
/// 覆盖。工具绝不持有规则的第二份副本。
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

    /// <summary>
    /// 难度阶梯 —— 三个旋钮都显式写在这里,调曲线是改配置 + 重新生成,不是改算法。
    /// <para>
    /// 阶梯是**数据给的**:tier1 里够挖一个字的有 890 条,tier2 加起来 9,615 条,
    /// 够挖两个字的 6,088 条。所以前四关只用最常用的那批,后四关才挖两个。
    /// </para>
    /// </summary>
    private static readonly GuessDifficultyDial[] Ladder =
    {
        new(PuzzleCount: 4, BlankCount: 1, MaxTier: 1),
        new(PuzzleCount: 5, BlankCount: 1, MaxTier: 1),
        new(PuzzleCount: 5, BlankCount: 1, MaxTier: 1),
        new(PuzzleCount: 6, BlankCount: 1, MaxTier: 1),
        new(PuzzleCount: 6, BlankCount: 1, MaxTier: 2),
        new(PuzzleCount: 6, BlankCount: 1, MaxTier: 2),
        new(PuzzleCount: 7, BlankCount: 1, MaxTier: 2),
        new(PuzzleCount: 7, BlankCount: 1, MaxTier: 2),
        new(PuzzleCount: 6, BlankCount: 2, MaxTier: 2),
        new(PuzzleCount: 7, BlankCount: 2, MaxTier: 2),
        new(PuzzleCount: 7, BlankCount: 2, MaxTier: 2),
        new(PuzzleCount: 8, BlankCount: 2, MaxTier: 2),
    };

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length is < 3 or > 4)
        {
            Console.Error.WriteLine(
                "usage: IdiomGuessGenerator <curated.json> <output.json> <seed> [generatedAt]");
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

        // 四字 + 有释义。生僻层没有释义,所以它自动落选 —— 而释义就是本游戏的题面。
        var all = curated.Idioms
            .Where(i => i.Word.Length == 4 && !string.IsNullOrWhiteSpace(i.Explanation))
            .Select(i => (Tier: i.Tier,
                          Idiom: new GuessSourceIdiom(i.Word, i.Explanation!, Blank(i.Derivation))))
            .ToList();

        var blankable = all
            .Count(t => IdiomGuessLevelGenerator.BlankablePositions(t.Idiom).Count > 0);

        Console.WriteLine($"four-character idioms with an explanation: {all.Count}");
        Console.WriteLine($"  of which at least one character is blankable: {blankable}");
        Console.WriteLine($"  rejected outright (every character is in its own explanation): {all.Count - blankable}");

        if (blankable < 200)
        {
            Console.Error.WriteLine("pool is too small to generate a level set.");
            return 1;
        }

        var used = new HashSet<string>(StringComparer.Ordinal);
        var levels = new List<LevelRecord>();

        for (var i = 0; i < Ladder.Length; i++)
        {
            var dial = Ladder[i];
            var corpus = all.Where(t => t.Tier <= dial.MaxTier).Select(t => t.Idiom);
            // 每一关一个派生种子,这样调整某一关的旋钮不会把它后面所有关都换掉。
            var generator = new IdiomGuessLevelGenerator(corpus, seed + i);
            var level = generator.Generate(dial, difficulty: i + 1, used);

            if (level.Layout.Puzzles.Count < dial.PuzzleCount)
            {
                Console.Error.WriteLine(
                    $"level {i + 1}: only {level.Layout.Puzzles.Count} of {dial.PuzzleCount} puzzles.");
            }

            levels.Add(new LevelRecord(i, i + 1, level.Layout, level.Solution));
            Console.WriteLine(
                $"level {i + 1}: {level.Layout.Puzzles.Count} puzzles, {dial.BlankCount} blank(s), tier ≤ {dial.MaxTier}");
        }

        var file = new LevelFile(
            Game: "idiom-guess",
            Seed: seed,
            DictionaryCommit: curated.SourceCommit,
            GeneratedAt: generatedAt,
            Dials: Ladder.Select(d =>
                $"puzzles={d.PuzzleCount},blanks={d.BlankCount},maxTier={d.MaxTier}").ToList(),
            Levels: levels);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        // 不传 Encoding —— `Encoding.UTF8` 会写 BOM,而另外两个关卡产物都没有。
        // 那三个字节不是风格问题:它让 JSON 解析器当场报错(实测 Python 直接抛),
        // 而产物在编辑器里看起来完全正常。末尾补一个换行,同样是跟着既有两个工具。
        File.WriteAllText(outputPath, JsonSerializer.Serialize(file, WriteOptions) + "\n");
        Console.WriteLine($"wrote {outputPath}");
        return 0;
    }

    private static string? Blank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
