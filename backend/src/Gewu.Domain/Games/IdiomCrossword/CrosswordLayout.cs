using System.Text.Json.Serialization;

namespace Gewu.Domain.Games.IdiomCrossword;

/// <summary>成语在网格中的方向。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CrosswordDirection
{
    /// <summary>横排,列递增。</summary>
    Horizontal = 0,

    /// <summary>竖排,行递增。</summary>
    Vertical = 1,
}

/// <summary>一个网格坐标。</summary>
/// <param name="Row">行,0 起。</param>
/// <param name="Col">列,0 起。</param>
public readonly record struct CrosswordCell(int Row, int Col);

/// <summary>一个预填格 —— 刻意公开自己的字,是让关卡有下手处的立足点。</summary>
/// <param name="Row">行。</param>
/// <param name="Col">列。</param>
/// <param name="Char">该格的字。</param>
public sealed record CrosswordGivenCell(int Row, int Col, string Char);

/// <summary>
/// 一个词槽:一条成语占据的连续格位。**不含**成语本身 —— 客户端靠它知道
/// "哪些格属于同一条成语",从而在填满时发起 <c>check</c>。
/// </summary>
/// <param name="Index">词槽下标,<c>check</c> 时用它指明校验哪一条。</param>
/// <param name="Row">首字所在行。</param>
/// <param name="Col">首字所在列。</param>
/// <param name="Direction">方向。</param>
/// <param name="Length">长度(当前恒为 4)。</param>
public sealed record CrosswordSlot(
    int Index,
    int Row,
    int Col,
    CrosswordDirection Direction,
    int Length)
{
    /// <summary>按方向展开该词槽占据的全部格位,顺序即读序。</summary>
    public IEnumerable<CrosswordCell> Cells()
    {
        for (var i = 0; i < Length; i++)
        {
            yield return Direction == CrosswordDirection.Horizontal
                ? new CrosswordCell(Row, Col + i)
                : new CrosswordCell(Row + i, Col);
        }
    }
}

/// <summary>
/// 下发给客户端的关卡布局。
/// <para>
/// 这里**没有**成语词、没有释义、没有非预填格的字。字盘不构成泄漏:它给出所需字符的
/// 多重集合外加干扰字,揭示的是"有哪些字"而非"哪个字放哪格" —— 后者才是谜题本身。
/// </para>
/// </summary>
/// <param name="Rows">网格行数。</param>
/// <param name="Cols">网格列数。</param>
/// <param name="Cells">存在的格位(其余位置在界面上是空洞)。</param>
/// <param name="Given">预填格及其字。</param>
/// <param name="Tray">字盘 —— 所需字符加干扰字,已打乱。</param>
/// <param name="Slots">词槽声明。</param>
public sealed record CrosswordLayout(
    int Rows,
    int Cols,
    IReadOnlyList<CrosswordCell> Cells,
    IReadOnlyList<CrosswordGivenCell> Given,
    IReadOnlyList<string> Tray,
    IReadOnlyList<CrosswordSlot> Slots);
