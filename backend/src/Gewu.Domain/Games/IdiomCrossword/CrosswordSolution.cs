namespace Gewu.Domain.Games.IdiomCrossword;

/// <summary>
/// 一条成语的答案与它的释义。释义只在玩家答对该条之后才被返回
/// (见 <c>PuzzlePartialResult.PayloadJson</c>)。
/// </summary>
/// <param name="Index">对应 <see cref="CrosswordSlot.Index"/>。</param>
/// <param name="Word">成语本身。</param>
/// <param name="Explanation">释义 —— 答对时展示的那张"纸条"。</param>
public sealed record CrosswordSolvedWord(int Index, string Word, string Explanation);

/// <summary>
/// 服务端答案。**永不下发客户端** —— 由 <c>PuzzleLevel.SolutionJson</c> 承载,
/// 校验、提示、计分都在服务端对它执行。
/// </summary>
/// <param name="Cells">每一格的正确字,键为 <c>"行,列"</c>。</param>
/// <param name="Words">每条成语的词与释义。</param>
public sealed record CrosswordSolution(
    IReadOnlyDictionary<string, string> Cells,
    IReadOnlyList<CrosswordSolvedWord> Words)
{
    /// <summary>把坐标编成 <see cref="Cells"/> 的键。</summary>
    /// <param name="row">行。</param>
    /// <param name="col">列。</param>
    public static string Key(int row, int col) => $"{row},{col}";

    /// <summary>取某格的正确字;该格不存在则 <c>null</c>。</summary>
    /// <param name="cell">格位。</param>
    public string? CharAt(CrosswordCell cell)
        => Cells.TryGetValue(Key(cell.Row, cell.Col), out var ch) ? ch : null;
}

/// <summary>
/// 玩家提交的完整网格:每格填了什么字。键与 <see cref="CrosswordSolution.Cells"/> 同构。
/// </summary>
/// <param name="Cells">玩家填入的字,键为 <c>"行,列"</c>;未填的格可以缺席。</param>
public sealed record CrosswordSubmission(IReadOnlyDictionary<string, string> Cells);

/// <summary>
/// 玩家对**一个词槽**的提交:填了哪一条成语。
/// </summary>
/// <param name="SlotIndex">要校验的词槽。</param>
/// <param name="Word">玩家在该词槽填出的字串。</param>
public sealed record CrosswordPartialSubmission(int SlotIndex, string Word);

/// <summary>一次提示揭示的内容:一个格位及其字。</summary>
/// <param name="Row">行。</param>
/// <param name="Col">列。</param>
/// <param name="Char">该格的字。</param>
public sealed record CrosswordRevealedCell(int Row, int Col, string Char);

/// <summary>
/// 客户端上报的盘面状态,给提示定位用。
/// <para>
/// 里面没有答案 —— 只有"哪些格已经有字"和"光标在哪",都是客户端自己看得见的东西。
/// 服务端据此决定揭哪一格,答案本身从不离开服务端。
/// </para>
/// </summary>
/// <param name="Filled">已填入字符的格位键(<c>"行,列"</c>)。</param>
/// <param name="Selected">当前选中的格位键;无选中则 <c>null</c>。</param>
public sealed record CrosswordHintState(
    IReadOnlyList<string>? Filled,
    string? Selected);
