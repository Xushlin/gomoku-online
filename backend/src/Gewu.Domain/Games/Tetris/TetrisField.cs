using Gewu.Domain.Exceptions;

namespace Gewu.Domain.Games.Tetris;

/// <summary>
/// 一块 10×20 的场地,以及在它上面放一个方块会发生什么。
/// <para>
/// 它是**公开的**,而这不是为测试开的后门:客户端必须能算落点才画得出硬降预览与锁定位置。
/// 把它做成一个明确的类型,也给 TypeScript 那一侧一个逐项对照的对象 ——
/// 比"照着重放代码再实现一遍"可靠。
/// </para>
/// </summary>
public sealed class TetrisField
{
    private readonly bool[,] _occupied = new bool[TetrisRules.Rows, TetrisRules.Columns];

    /// <summary>某格是否已被占。</summary>
    /// <param name="row">行,0 在顶。</param>
    /// <param name="col">列。</param>
    public bool IsOccupied(int row, int col) => _occupied[row, col];

    /// <summary>
    /// 这个方块以该旋转态从该列落下,会停在哪一行(返回它最上一格的行);放不进去返回 <c>null</c>。
    /// </summary>
    /// <param name="kind">方块。</param>
    /// <param name="rotation">旋转态。</param>
    /// <param name="column">最左格所在列。</param>
    /// <exception cref="InvalidMoveException">该列放不下这个宽度 —— 越界与"堆太高"是两件事,分开报。</exception>
    public int? LandingRow(TetrominoKind kind, int rotation, int column)
    {
        var cells = Tetromino.CellsOf(kind, rotation);
        var width = Tetromino.WidthOf(kind, rotation);

        if (column < 0 || column + width > TetrisRules.Columns)
        {
            throw new InvalidMoveException(
                $"{kind} rotation {rotation} at column {column} does not fit the field.");
        }

        var height = cells.Max(c => c.Row) + 1;
        int? landing = null;
        for (var top = 0; top + height <= TetrisRules.Rows; top++)
        {
            if (Collides(cells, top, column))
            {
                break;
            }
            landing = top;
        }
        return landing;
    }

    /// <summary>把方块落下去并消行,返回消掉的行数。</summary>
    /// <param name="kind">方块。</param>
    /// <param name="rotation">旋转态。</param>
    /// <param name="column">最左格所在列。</param>
    /// <exception cref="InvalidMoveException">越界,或堆得太高放不下。</exception>
    public int PlaceAndClear(TetrominoKind kind, int rotation, int column)
    {
        var landing = LandingRow(kind, rotation, column)
            ?? throw new InvalidMoveException(
                $"{kind} cannot be placed at column {column}; the stack is too high.");

        foreach (var (dr, dc) in Tetromino.CellsOf(kind, rotation))
        {
            _occupied[landing + dr, column + dc] = true;
        }

        return ClearFullLines();
    }

    private bool Collides(IReadOnlyList<(int Row, int Col)> cells, int top, int column)
    {
        foreach (var (dr, dc) in cells)
        {
            if (_occupied[top + dr, column + dc]) return true;
        }
        return false;
    }

    private int ClearFullLines()
    {
        var cleared = 0;
        for (var row = TetrisRules.Rows - 1; row >= 0; row--)
        {
            var full = true;
            for (var col = 0; col < TetrisRules.Columns; col++)
            {
                if (!_occupied[row, col]) { full = false; break; }
            }
            if (!full) continue;

            cleared++;
            for (var r = row; r > 0; r--)
            {
                for (var col = 0; col < TetrisRules.Columns; col++)
                {
                    _occupied[r, col] = _occupied[r - 1, col];
                }
            }
            for (var col = 0; col < TetrisRules.Columns; col++) _occupied[0, col] = false;
            row++; // 同一行现在是原来它上面那行
        }
        return cleared;
    }
}
