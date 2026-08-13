using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gewu.Domain.Idioms;

namespace Gewu.Tools.IdiomImporter;

/// <summary>上游 <c>idiom.json</c> 的一条记录。</summary>
internal sealed record UpstreamIdiom(
    [property: JsonPropertyName("word")] string Word,
    [property: JsonPropertyName("pinyin")] string? Pinyin,
    [property: JsonPropertyName("explanation")] string? Explanation,
    [property: JsonPropertyName("derivation")] string? Derivation,
    [property: JsonPropertyName("example")] string? Example);

/// <summary>
/// 把上游 chinese-xinhua 的成语数据转成 <c>backend/data/idioms.curated.json</c>。
///
/// 用法:
///   dotnet run --project backend/tools/IdiomImporter -- &lt;upstream-idiom.json&gt; &lt;output.json&gt; &lt;source-commit-sha&gt;
///
/// 产物策略(与 design.md D1/D3 一致):
///   * 保留**全部**词条 —— 成语接龙要能认出玩家答的冷僻但合法的成语,砍掉生僻层会造成误判。
///   * 释义 / 出处 / 例句正文只对 tier 1–2 保留 —— 生僻层永远不会被展示,留着白占 3 MB。
///   * 按 <c>word</c> 排序、每条一行输出 —— 重新导入后 diff 能精确显示哪几条改了层级。
///   * 写入 <c>tier</c>,但种子载入时会用 <see cref="IdiomTiering.Classify"/> 重算校验,
///     两边不一致就报错,避免出现两份分层真源。
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                "usage: IdiomImporter <upstream-idiom.json> <output.json> <source-commit-sha>");
            return 1;
        }

        var (inputPath, outputPath, commit) = (args[0], args[1], args[2]);

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"upstream file not found: {inputPath}");
            return 1;
        }

        var upstream = JsonSerializer.Deserialize<List<UpstreamIdiom>>(
            File.ReadAllText(inputPath), ReadOptions);
        if (upstream is null || upstream.Count == 0)
        {
            Console.Error.WriteLine("upstream file deserialised to nothing.");
            return 1;
        }

        Console.WriteLine($"read {upstream.Count:N0} upstream idioms");

        // 字频代理:一个字出现在多少条成语里。上游不含任何词频数据,而 word.json 的
        // 16,142 字近乎涵盖全部汉字、无法用作常用字筛选,所以从语料自身统计。
        var documentFrequency = new Dictionary<char, int>();
        foreach (var entry in upstream)
        {
            foreach (var ch in entry.Word.Distinct())
            {
                documentFrequency[ch] = documentFrequency.GetValueOrDefault(ch) + 1;
            }
        }

        Console.WriteLine($"distinct characters: {documentFrequency.Count:N0}");

        var curated = new List<CuratedRow>(upstream.Count);
        foreach (var entry in upstream)
        {
            var word = entry.Word.Trim();
            if (word.Length == 0)
            {
                continue;
            }

            var minFrequency = word.Min(ch => documentFrequency.GetValueOrDefault(ch));
            var tier = IdiomTiering.Classify(
                word.Length,
                IdiomTiering.HasContent(entry.Example),
                IdiomTiering.HasContent(entry.Derivation),
                minFrequency);

            // 生僻层只保留校验所需的最小信息,正文一律丢弃。
            var keepProse = tier != IdiomTier.Obscure;
            curated.Add(new CuratedRow(
                word,
                entry.Pinyin?.Trim() ?? string.Empty,
                minFrequency,
                (int)tier,
                keepProse ? Clean(entry.Explanation) : null,
                keepProse ? Clean(entry.Derivation) : null,
                keepProse ? Clean(entry.Example) : null));
        }

        Report(curated);

        curated.Sort((a, b) => string.CompareOrdinal(a.Word, b.Word));
        WriteArtefact(outputPath, commit, curated, documentFrequency.Count);

        var sizeMb = new FileInfo(outputPath).Length / 1048576.0;
        Console.WriteLine($"wrote {outputPath} ({sizeMb:F2} MB, {curated.Count:N0} idioms)");
        return 0;
    }

    /// <summary>打印层级分布与每层样例 —— 阈值靠看这份报告选,不是拍脑袋。</summary>
    private static void Report(List<CuratedRow> rows)
    {
        Console.WriteLine();
        Console.WriteLine("=== tier distribution ===");
        foreach (var tier in new[] { 1, 2, 3 })
        {
            var inTier = rows.Where(r => r.Tier == tier).ToList();
            var share = inTier.Count * 100.0 / rows.Count;
            Console.WriteLine($"  tier {tier}: {inTier.Count,6:N0}  ({share,5:F1}%)");

            // 确定性取样:按词排序后等距抽,输出稳定可比对。
            var sorted = inTier.OrderBy(r => r.Word, StringComparer.Ordinal).ToList();
            var step = Math.Max(1, sorted.Count / 20);
            var sample = Enumerable.Range(0, 20)
                .Select(k => k * step)
                .Where(idx => idx < sorted.Count)
                .Select(idx => sorted[idx].Word);
            Console.WriteLine($"          {string.Join(' ', sample)}");
        }

        Console.WriteLine();
        Console.WriteLine("thresholds: " +
            $"tier1 needs {IdiomTiering.PreferredCharCount} chars + example + derivation + " +
            $"minCharFrequency >= {IdiomTiering.CommonMinCharFrequency}; " +
            $"tier2 needs (example | derivation) + minCharFrequency >= {IdiomTiering.UsableMinCharFrequency}");
        Console.WriteLine();
    }

    private static void WriteArtefact(
        string outputPath,
        string commit,
        List<CuratedRow> rows,
        int distinctCharacters)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 手写 JSON 而不用序列化器:一条一行是刻意的格式选择,它让重新导入后的
        // diff 精确到"哪几条成语改了层级",这正是把产物提交进仓库的意义所在。
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append("  \"source\": \"https://github.com/pwxcoo/chinese-xinhua\",\n");
        sb.Append($"  \"sourceCommit\": \"{commit}\",\n");
        sb.Append("  \"license\": \"MIT\",\n");
        sb.Append($"  \"corpusIdioms\": {rows.Count},\n");
        sb.Append($"  \"corpusDistinctCharacters\": {distinctCharacters},\n");
        sb.Append($"  \"tier1MinCharFrequency\": {IdiomTiering.CommonMinCharFrequency},\n");
        sb.Append($"  \"tier2MinCharFrequency\": {IdiomTiering.UsableMinCharFrequency},\n");
        sb.Append("  \"proseRetainedForTiers\": [1, 2],\n");
        sb.Append("  \"idioms\": [\n");

        for (var i = 0; i < rows.Count; i++)
        {
            sb.Append("    ");
            sb.Append(JsonSerializer.Serialize(rows[i], WriteOptions));
            sb.Append(i == rows.Count - 1 ? "\n" : ",\n");
        }

        sb.Append("  ]\n");
        sb.Append("}\n");

        File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
    }

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        // 中文原样写出,不转成 \uXXXX —— 转义后产物既不可读也无法 diff。
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string? Clean(string? value)
        => IdiomTiering.HasContent(value) ? value!.Trim() : null;

    private sealed record CuratedRow(
        [property: JsonPropertyName("word")] string Word,
        [property: JsonPropertyName("pinyin")] string Pinyin,
        [property: JsonPropertyName("minCharFrequency")] int MinCharFrequency,
        [property: JsonPropertyName("tier")] int Tier,
        [property: JsonPropertyName("explanation")] string? Explanation,
        [property: JsonPropertyName("derivation")] string? Derivation,
        [property: JsonPropertyName("example")] string? Example);
}
