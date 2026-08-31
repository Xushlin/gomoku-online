using System.Text.Encodings.Web;
using System.Text.Json;
using Gewu.Domain.Puzzles;

namespace Gewu.Domain.Games.IdiomGuess;

/// <summary>
/// 猜成语的规则 —— 平台的第三个 <see cref="IPuzzleRules"/> 实现。
/// <para>
/// 键写成字面量而不是 <c>GameKeys</c> 常量,是照着这里的既定做法:<c>GameKeys</c> 的文档
/// 写着它是「平台内置**棋种**的键」,而两个关卡类游戏(成语纵横、华容道)都在自己的规则
/// 类里写字面量。往那个类里塞一个关卡游戏会让它的说明当场变假。
/// </para>
/// <para>
/// <b>这个实现证明不了 <see cref="IPuzzleRules"/> 更通用了,这一点要说在前面。</b> 它与
/// 成语纵横**同族** —— 同一本词典、同样往格子里填字、关卡同样是提交进仓库的产物。
/// 它能兑现的是「一个实现 + 一处注册」那条**改动面**判据;真正检验通用性的,要等一个
/// 不填格子的关卡游戏。上一次没写这句,代价是一个假的「已证明」在规格里躺了很久。
/// </para>
/// </summary>
public sealed class IdiomGuessRules : IPuzzleRules
{
    /// <summary>与 API 的 camelCase 约定一致;中文不转义,理由同成语纵横。</summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <inheritdoc />
    public string GameKey => "idiom-guess";

    /// <inheritdoc />
    public PuzzleValidationResult Validate(
        string solutionJson, string layoutJson, string submissionJson)
    {
        var solution = TryDeserialize<IdiomGuessSolution>(solutionJson);
        var submission = TryDeserialize<IdiomGuessSubmission>(submissionJson);
        if (solution is null || submission?.Words is null)
        {
            return new PuzzleValidationResult(false);
        }

        // 全对才算通关 —— 少答、答错都不通过。
        foreach (var answer in solution.Puzzles)
        {
            if (!submission.Words.TryGetValue(
                    answer.Index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    out var given)
                || !string.Equals(given, answer.Word, StringComparison.Ordinal))
            {
                return new PuzzleValidationResult(false);
            }
        }

        return new PuzzleValidationResult(true);
    }

    /// <inheritdoc />
    public PuzzlePartialResult CheckPartial(
        string solutionJson, string layoutJson, string partialJson)
    {
        var solution = TryDeserialize<IdiomGuessSolution>(solutionJson);
        var partial = TryDeserialize<IdiomGuessPartialSubmission>(partialJson);
        if (solution is null || partial is null)
        {
            return new PuzzlePartialResult(false);
        }

        var answer = solution.Puzzles.FirstOrDefault(p => p.Index == partial.PuzzleIndex);
        if (answer is null || !string.Equals(partial.Word, answer.Word, StringComparison.Ordinal))
        {
            // 答错 MUST NOT 附带载荷 —— 否则错误路径就成了泄题通道。这是接口写下的规矩。
            return new PuzzlePartialResult(false);
        }

        // 答对:回传这条成语与它的**出处**。
        //
        // 出处**可能没有** —— 可用池 9,615 条里有 252 条为空。这里照实回 null,而不是
        // 编一个空串:一张空纸条在屏幕上和"加载失败"长得一样,而客户端只有拿到 null
        // 才知道该不画。
        var payload = JsonSerializer.Serialize(
            new IdiomGuessSolved(answer.Index, answer.Word, NullIfBlank(answer.Derivation)), Json);

        return new PuzzlePartialResult(true, payload);
    }

    /// <inheritdoc />
    public PuzzleHintResult Hint(string solutionJson, string layoutJson, string? stateJson)
    {
        var solution = TryDeserialize<IdiomGuessSolution>(solutionJson);
        var layout = TryDeserialize<IdiomGuessLayout>(layoutJson);
        if (solution is null || layout is null)
        {
            return new PuzzleHintResult("{}");
        }

        // 所有还空着的格,按题号、位置排序 —— 顺序确定,提示才可预期。
        var blanks = layout.Puzzles
            .OrderBy(p => p.Index)
            .SelectMany(p => p.Chars
                .Select((c, pos) => (Puzzle: p.Index, Position: pos, Char: c))
                .Where(t => t.Char is null))
            .ToList();

        if (blanks.Count == 0)
        {
            return new PuzzleHintResult("{}");
        }

        var state = TryDeserialize<IdiomGuessHintState>(stateJson ?? string.Empty);
        var filled = state?.Filled is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : state.Filled.ToHashSet(StringComparer.Ordinal);

        // 「找到了没」用**下标**表达,不用 `default`。
        //
        // 这些空格是值元组,而 `default` 是 `(0, 0, null)` —— 一个**合法**的空格(第 0 题
        // 第 0 位)。拿 `== default` 当"没找到"的哨兵,会在题目恰好是那一格时把它当成
        // 没找到。隔壁 `IdiomCrosswordRules` 为同一件事留过一段注释,而我第一版照样踩了。
        var at = -1;

        // ① 玩家指着哪一格就揭哪一格 —— 与成语纵横同一条:原型就让玩家点着某格要提示,
        //    而每次照样扣一颗星。指的格子不存在(比如重开后残留的光标)时退到 ②,
        //    **不报错** —— 一个没更新的客户端应该拿到提示,而不是 400。
        if (state?.Selected is { } key)
        {
            at = blanks.FindIndex(b => Key(b.Puzzle, b.Position) == key);
        }

        // ② 否则揭第一个玩家还没填的空。
        if (at < 0)
        {
            at = blanks.FindIndex(b => !filled.Contains(Key(b.Puzzle, b.Position)));
        }

        // ③ 全填满了(通常是填错了)就揭第一个空 —— 用正确的字盖掉它。
        if (at < 0)
        {
            at = 0;
        }

        var target = blanks[at];
        var word = solution.Puzzles.FirstOrDefault(p => p.Index == target.Puzzle)?.Word;
        if (word is null || target.Position >= word.Length)
        {
            return new PuzzleHintResult("{}");
        }

        return new PuzzleHintResult(JsonSerializer.Serialize(
            new IdiomGuessRevealed(
                target.Puzzle, target.Position, word[target.Position].ToString()),
            Json));
    }

    /// <inheritdoc />
    public int Score(PuzzleScoreInput input)
    {
        // 与成语纵横同一个公式,而这是**判断**不是复制:两个游戏的成绩都是"错了几次、
        // 要了几次提示"。用时不参与 —— 想清楚每一步的玩家不该因为想得慢而掉星。
        // 提交也不参与:填在哪儿不额外说明什么(华容道要看提交,是因为它计步数)。
        var cost = input.HintsUsed + input.Mistakes;
        return cost == 0 ? 3 : cost <= 2 ? 2 : 1;
    }

    /// <summary>提示状态里那个键的写法:题号:位置。</summary>
    internal static string Key(int puzzleIndex, int position)
        => $"{puzzleIndex}:{position}";

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static T? TryDeserialize<T>(string json) where T : class
    {
        // 载荷来自玩家,畸形输入是正常情况:一律当作"不正确",不让坏 JSON 变成 500。
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
