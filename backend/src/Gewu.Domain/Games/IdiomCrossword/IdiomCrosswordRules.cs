using System.Text.Encodings.Web;
using System.Text.Json;
using Gewu.Domain.Puzzles;

namespace Gewu.Domain.Games.IdiomCrossword;

/// <summary>
/// 成语纵横的规则 —— 平台的第一个 <see cref="IPuzzleRules"/> 实现。
/// <para>
/// 计分公式照搬原型:<c>cost = mistakes + hintsUsed</c>,0 → 3 星、≤2 → 2 星、否则 1 星。
/// 三个入参都由服务端产生(提示由它发放、错误由它在部分校验里判定、时钟是它自己的),
/// 本实现 MUST NOT 引入任何其它信号。
/// </para>
/// </summary>
public sealed class IdiomCrosswordRules : IPuzzleRules
{
    /// <summary>
    /// JSON 选项:与 API 的 camelCase 约定一致,便于载荷直接被前端消费。
    /// <para>
    /// 中文不转义 —— 载荷是"字符串里套 JSON"(平台不理解各游戏的内容,所以只能原样透传),
    /// 默认转义会把每个汉字变成 6 字节的 <c>\uXXXX</c>,既让响应体膨胀,也让日志和调试
    /// 里的成语变成没法读的东西。与生成器写产物时的选择保持一致。
    /// </para>
    /// </summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <inheritdoc />
    public string GameKey => "idiom-crossword";

    /// <inheritdoc />
    public PuzzleValidationResult Validate(string solutionJson, string submissionJson)
    {
        var solution = Deserialize<CrosswordSolution>(solutionJson);
        var submission = TryDeserialize<CrosswordSubmission>(submissionJson);
        if (solution is null || submission is null)
        {
            return new PuzzleValidationResult(false);
        }

        // 全对才算通关:格数必须相符,且每一格逐字一致。少填、多填、填错都不通过。
        if (submission.Cells.Count != solution.Cells.Count)
        {
            return new PuzzleValidationResult(false);
        }

        foreach (var (key, expected) in solution.Cells)
        {
            if (!submission.Cells.TryGetValue(key, out var actual) || actual != expected)
            {
                return new PuzzleValidationResult(false);
            }
        }

        return new PuzzleValidationResult(true);
    }

    /// <inheritdoc />
    public PuzzlePartialResult CheckPartial(string solutionJson, string partialJson)
    {
        var solution = Deserialize<CrosswordSolution>(solutionJson);
        var partial = TryDeserialize<CrosswordPartialSubmission>(partialJson);
        if (solution is null || partial is null)
        {
            return new PuzzlePartialResult(false);
        }

        var word = solution.Words.FirstOrDefault(w => w.Index == partial.SlotIndex);
        if (word is null || partial.Word != word.Word)
        {
            // 答错 MUST NOT 附带载荷 —— 否则错误路径就成了泄题通道。
            return new PuzzlePartialResult(false);
        }

        // 答对:回传这条成语与它的释义,前端据此弹出那张"纸条"。载荷描述的是玩家刚刚
        // 已经解开的那部分,不透露网格未解部分的任何信息。
        var payload = JsonSerializer.Serialize(
            new CrosswordSolvedWord(word.Index, word.Word, word.Explanation), Json);

        return new PuzzlePartialResult(true, payload);
    }

    /// <inheritdoc />
    public PuzzleHintResult Hint(string solutionJson, string layoutJson, int alreadyRevealedCount)
    {
        var solution = Deserialize<CrosswordSolution>(solutionJson);
        var layout = Deserialize<CrosswordLayout>(layoutJson);
        if (solution is null || layout is null)
        {
            return new PuzzleHintResult("{}");
        }

        // 按阅读顺序(行优先)揭示第 N 个非预填格。不接收玩家的光标位置 —— 那要靠客户端
        // 自述并被信任,对一个上排行榜的游戏是反方向的取舍。
        var given = layout.Given
            .Select(g => CrosswordSolution.Key(g.Row, g.Col))
            .ToHashSet(StringComparer.Ordinal);

        var revealable = layout.Cells
            .OrderBy(c => c.Row)
            .ThenBy(c => c.Col)
            .Where(c => !given.Contains(CrosswordSolution.Key(c.Row, c.Col)))
            .ToList();

        if (revealable.Count == 0)
        {
            return new PuzzleHintResult("{}");
        }

        // 提示用尽后夹到最后一格,而不是抛错 —— 多要一次提示只是浪费,不该是个错误。
        var index = Math.Clamp(alreadyRevealedCount, 0, revealable.Count - 1);
        var cell = revealable[index];
        var ch = solution.CharAt(cell) ?? string.Empty;

        return new PuzzleHintResult(
            JsonSerializer.Serialize(new CrosswordRevealedCell(cell.Row, cell.Col, ch), Json));
    }

    /// <inheritdoc />
    public int Score(int hintsUsed, int mistakes, TimeSpan duration)
    {
        // 与原型一致:用时不参与计分。它被记录下来做最好成绩的次级排序,但一个想清楚
        // 每一步的玩家不该因为想得慢而掉星。
        var cost = hintsUsed + mistakes;
        return cost == 0 ? 3 : cost <= 2 ? 2 : 1;
    }

    private static T? Deserialize<T>(string json) where T : class
        => TryDeserialize<T>(json);

    private static T? TryDeserialize<T>(string json) where T : class
    {
        // 载荷来自玩家,畸形输入是正常情况而不是异常情况:一律当作"不正确"处理,
        // 不让一个坏 JSON 变成 500。
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
