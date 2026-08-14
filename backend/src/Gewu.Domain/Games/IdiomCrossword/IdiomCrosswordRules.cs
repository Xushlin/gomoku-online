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
    public PuzzleHintResult Hint(string solutionJson, string layoutJson, string? stateJson)
    {
        var solution = Deserialize<CrosswordSolution>(solutionJson);
        var layout = Deserialize<CrosswordLayout>(layoutJson);
        if (solution is null || layout is null)
        {
            return new PuzzleHintResult("{}");
        }

        var given = layout.Given
            .Select(g => CrosswordSolution.Key(g.Row, g.Col))
            .ToHashSet(StringComparer.Ordinal);

        // 阅读顺序(行优先)下所有可揭示的格 —— 预填格永远不在其中。
        var revealable = layout.Cells
            .OrderBy(c => c.Row)
            .ThenBy(c => c.Col)
            .Where(c => !given.Contains(CrosswordSolution.Key(c.Row, c.Col)))
            .ToList();

        if (revealable.Count == 0)
        {
            return new PuzzleHintResult("{}");
        }

        var state = TryDeserialize<CrosswordHintState>(stateJson ?? string.Empty);
        var filled = state?.Filled is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : state.Filled.ToHashSet(StringComparer.Ordinal);

        // 注意 CrosswordCell 是 record struct —— `default` 就是合法的 (0,0),所以
        // "找到了没"必须用可空表达,不能拿 `!= default` 当哨兵。
        CrosswordCell? Find(Func<CrosswordCell, bool> match)
        {
            foreach (var c in revealable)
            {
                if (match(c))
                {
                    return c;
                }
            }
            return null;
        }

        // ① 玩家指着哪一格就揭哪一格 —— 与原型一致。即使那格已经有字也照揭:
        //    盯着一个填错的格子要提示,想解的正是那一格,客户端会先把错字块退回字盘。
        //    选中格不存在或是预填格时忽略(它不在 revealable 里),退到 ② ——
        //    重开后残留的光标应该降级成一个合理的提示,而不是一个错误。
        var cell = state?.Selected is { } key
            ? Find(c => CrosswordSolution.Key(c.Row, c.Col) == key)
            : null;

        // ② 否则揭阅读顺序上第一个玩家还没填的格 —— 这正是修掉"提示总是揭已解开的格"的地方。
        cell ??= Find(c => !filled.Contains(CrosswordSolution.Key(c.Row, c.Col)));

        // ③ 满盘皆填(通常是填错了)时退到第一个可揭示格 —— 用正确字覆盖它,
        //    这正是满盘时最有用的一步。
        var target = cell ?? revealable[0];

        var ch = solution.CharAt(target) ?? string.Empty;

        return new PuzzleHintResult(
            JsonSerializer.Serialize(new CrosswordRevealedCell(target.Row, target.Col, ch), Json));
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
