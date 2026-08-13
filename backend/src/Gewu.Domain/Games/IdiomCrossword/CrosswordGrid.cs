
namespace Gewu.Domain.Games.IdiomCrossword;

/// <summary>词典中一条可用于出题的四字成语。</summary>
/// <param name="Word">成语。</param>
/// <param name="Explanation">释义 —— 答对时展示。</param>
public sealed record SourceIdiom(string Word, string Explanation);

/// <summary>一个难度档位的三个旋钮。</summary>
/// <param name="IdiomCount">目标成语条数。</param>
/// <param name="GivenCount">预填格数。</param>
/// <param name="DistractorCount">干扰字数。</param>
public sealed record DifficultyDial(int IdiomCount, int GivenCount, int DistractorCount);

/// <summary>网格中一条已摆放的成语。</summary>
public sealed record PlacedWord(
    int Index,
    SourceIdiom Idiom,
    int Row,
    int Col,
    CrosswordDirection Direction)
{
    /// <summary>该成语占据的格位,顺序即读序。</summary>
    public IEnumerable<CrosswordCell> Cells()
    {
        for (var i = 0; i < Idiom.Word.Length; i++)
        {
            yield return Direction == CrosswordDirection.Horizontal
                ? new CrosswordCell(Row, Col + i)
                : new CrosswordCell(Row + i, Col);
        }
    }
}

/// <summary>
/// 一个关卡的网格构建器。
/// <para>
/// 核心正确性属性在 <see cref="CanPlace"/>:摆放一条垂直交叉的成语时,除共用的交叉格
/// 以外,新占用的每一格都不得有已被占用的正交邻格。少了这一条,生成器会愉快地产出
/// "两条成语平行只隔一格"的网格 —— 并排的字连读起来是无意义的串,玩家无法把它跟真正的
/// 约束区分开。谜题仍然"可解",但已经坏了。
/// </para>
/// </summary>
public sealed class CrosswordGrid
{
    private readonly Dictionary<CrosswordCell, char> _cells = new();
    private readonly List<PlacedWord> _words = new();

    /// <summary>已摆放的成语。</summary>
    public IReadOnlyList<PlacedWord> Words => _words;

    /// <summary>已占用的格位及其字。</summary>
    public IReadOnlyDictionary<CrosswordCell, char> Cells => _cells;

    /// <summary>摆下第一条成语(横排),作为整张网格的锚。</summary>
    public void PlaceSeed(SourceIdiom idiom)
    {
        if (_words.Count > 0)
        {
            throw new InvalidOperationException("Seed must be the first placement.");
        }

        Commit(new PlacedWord(0, idiom, 0, 0, CrosswordDirection.Horizontal));
    }

    /// <summary>
    /// 判断一条成语能否摆在给定位置。
    /// </summary>
    /// <param name="candidate">候选摆放。</param>
    /// <param name="intersection">与已有成语共用的那一格。</param>
    public bool CanPlace(PlacedWord candidate, CrosswordCell intersection)
    {
        var word = candidate.Idiom.Word;
        var cells = candidate.Cells().ToList();

        // 新词自己的格位集合 —— 相邻性检查要把它们排除在外,否则同一条成语内部
        // 沿词方向的相邻会把每一次摆放都判为非法。
        var own = cells.ToHashSet();

        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var expected = word[i];

            if (cell == intersection)
            {
                // 交叉格必须已被占用且字相符 —— 这正是它存在的理由,不受相邻规则约束。
                if (!_cells.TryGetValue(cell, out var existing) || existing != expected)
                {
                    return false;
                }
                continue;
            }

            // 非交叉格必须是空的。
            if (_cells.ContainsKey(cell))
            {
                return false;
            }

            // 且不得有任何已占用的正交邻格(自己这条词的格子不算)。
            foreach (var neighbour in Neighbours(cell))
            {
                if (own.Contains(neighbour))
                {
                    continue;
                }
                if (_cells.ContainsKey(neighbour))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>摆放一条成语。调用方 MUST 先用 <see cref="CanPlace"/> 校验。</summary>
    /// <param name="idiom">成语。</param>
    /// <param name="row">首字行。</param>
    /// <param name="col">首字列。</param>
    /// <param name="direction">方向。</param>
    public PlacedWord Place(SourceIdiom idiom, int row, int col, CrosswordDirection direction)
    {
        var placed = new PlacedWord(_words.Count, idiom, row, col, direction);
        Commit(placed);
        return placed;
    }

    /// <summary>某格属于几条成语 —— 值为 2 表示它是交叉格。</summary>
    /// <param name="cell">格位。</param>
    public int WordCountAt(CrosswordCell cell)
        => _words.Count(w => w.Cells().Contains(cell));

    /// <summary>
    /// 重新验证整张网格的相邻不变式。生成器在摆放时已经校验过,这里是**独立的一道审计**
    /// —— 这条性质最容易被一个看起来没问题的实现悄悄破坏,所以它值得被检查两次。
    /// </summary>
    public bool SatisfiesAdjacencyInvariant()
    {
        foreach (var word in _words)
        {
            var own = word.Cells().ToHashSet();
            foreach (var cell in own)
            {
                // 交叉格豁免。
                if (WordCountAt(cell) > 1)
                {
                    continue;
                }

                foreach (var neighbour in Neighbours(cell))
                {
                    if (own.Contains(neighbour))
                    {
                        continue;
                    }
                    if (_cells.ContainsKey(neighbour))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private void Commit(PlacedWord placed)
    {
        var cells = placed.Cells().ToList();
        for (var i = 0; i < cells.Count; i++)
        {
            _cells[cells[i]] = placed.Idiom.Word[i];
        }
        _words.Add(placed);
    }

    private static IEnumerable<CrosswordCell> Neighbours(CrosswordCell cell)
    {
        yield return new CrosswordCell(cell.Row - 1, cell.Col);
        yield return new CrosswordCell(cell.Row + 1, cell.Col);
        yield return new CrosswordCell(cell.Row, cell.Col - 1);
        yield return new CrosswordCell(cell.Row, cell.Col + 1);
    }
}
