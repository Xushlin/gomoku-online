using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Entities;

/// <summary>
/// 「连 N 子」类棋盘。维护棋格状态,提供落子、查询、判胜、克隆与重置。
/// 判胜采用"只检查刚落子"的增量算法,单次 <see cref="PlaceStone"/> 为 O(1)。
/// 非线程安全 —— 约定在单线程下使用(一盘对局一个实例)。
/// <para>
/// <see cref="Rows"/> / <see cref="Cols"/> / <see cref="WinLength"/> 是**构造参数**
/// 而非编译期常量:这三个数是棋种属性,由 <c>IGameRules</c> 提供。五子棋是
/// (15, 15, 5),一字棋是 (3, 3, 3),判胜算法一字不差。
/// </para>
/// <para>
/// 调用方(Application、AI、SignalR Hub)应在调用 <see cref="PlaceStone"/> 之前自行校验落点合法性。
/// 本类抛出的 <see cref="InvalidMoveException"/> 仅用于保护 Domain 不变量,
/// 不应作为"某位置是否能落子"的查询手段。
/// </para>
/// </summary>
public sealed class Board
{
    private readonly Stone[] _cells;

    /// <summary>行数。</summary>
    public int Rows { get; }

    /// <summary>列数。</summary>
    public int Cols { get; }

    /// <summary>判胜所需的同色连续子数。</summary>
    public int WinLength { get; }

    /// <summary>格子总数。</summary>
    public int CellCount => Rows * Cols;

    /// <summary>
    /// 构造一块空棋盘。
    /// </summary>
    /// <param name="rows">行数,必须为正。</param>
    /// <param name="cols">列数,必须为正。</param>
    /// <param name="winLength">连子长度,必须为正且不超过 <c>max(rows, cols)</c>。</param>
    /// <exception cref="ArgumentOutOfRangeException">任一参数不合法。</exception>
    public Board(int rows, int cols, int winLength)
    {
        if (rows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), rows, "Rows must be positive.");
        }
        if (cols <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cols), cols, "Cols must be positive.");
        }
        if (winLength <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(winLength), winLength, "Win length must be positive.");
        }
        if (winLength > Math.Max(rows, cols))
        {
            // 赢不了的棋种是配置错误,不是一盘可玩的棋。
            throw new ArgumentOutOfRangeException(
                nameof(winLength),
                winLength,
                $"Win length {winLength} cannot exceed the longest side of a {rows}×{cols} board.");
        }

        Rows = rows;
        Cols = cols;
        WinLength = winLength;
        _cells = new Stone[rows * cols];
    }

    /// <summary>该坐标是否落在本棋盘内。</summary>
    /// <param name="position">坐标。</param>
    public bool Contains(Position position)
        => position.Row < Rows && position.Col < Cols;

    /// <summary>查询指定位置的棋子。</summary>
    /// <param name="position">本棋盘内的坐标。</param>
    /// <exception cref="InvalidMoveException">坐标超出本棋盘范围。</exception>
    public Stone GetStone(Position position)
    {
        return _cells[IndexOf(position)];
    }

    /// <summary>
    /// 原子化地放下一子并判定对局结果。流程:
    /// (1) 校验目标格为空 →
    /// (2) 写入 <paramref name="move"/> 的棋色 →
    /// (3) 以该落子为中心沿 4 个方向增量判胜 →
    /// (4) 若未决胜且棋盘已满则判平,否则返回 <see cref="GameResult.Ongoing"/>。
    /// </summary>
    /// <param name="move">一次合法落子;<see cref="Move.Stone"/> 必为黑或白。</param>
    /// <returns>落子之后的对局状态。</returns>
    /// <exception cref="InvalidMoveException">坐标越界,或目标格已有棋子。棋盘状态保持不变。</exception>
    public GameResult PlaceStone(Move move)
    {
        var index = IndexOf(move.Position);

        if (_cells[index] != Stone.Empty)
        {
            throw new InvalidMoveException(
                $"Position ({move.Position.Row}, {move.Position.Col}) is already occupied by {_cells[index]}.");
        }

        _cells[index] = move.Stone;

        if (FormsWin(move.Position, move.Stone))
        {
            // 赢的一方**就是** move.Stone —— 落子类棋种里落子的人不可能因为落子而输。
            // 所以返回值里不再重复一遍那个颜色:调用方手上就有 `move`,而此前那句
            // `move.Stone == Black ? BlackWin : WhiteWin` 是把入参重新说了一遍。
            return GameResult.Decided;
        }

        return IsFull() ? GameResult.Draw : GameResult.Ongoing;
    }

    /// <summary>返回一份完全独立的棋盘副本,供 AI 搜索等"试走"场景使用。尺寸一并保留。</summary>
    public Board Clone()
    {
        var clone = new Board(Rows, Cols, WinLength);
        Array.Copy(_cells, clone._cells, _cells.Length);
        return clone;
    }

    /// <summary>把棋盘恢复为初始空盘。</summary>
    public void Reset()
    {
        Array.Clear(_cells);
    }

    /// <summary>行优先线性下标。用 <see cref="Cols"/> 换算 —— 不得残留"边长"式的方形假设。</summary>
    private int IndexOf(Position position)
    {
        if (!Contains(position))
        {
            throw new InvalidMoveException(
                $"Position ({position.Row}, {position.Col}) is outside this {Rows}×{Cols} board.");
        }

        return position.Row * Cols + position.Col;
    }

    private bool IsFull()
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] == Stone.Empty)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 以 <paramref name="last"/> 为中心,沿水平、竖直、主对角(↘)、反对角(↗)
    /// 四个方向各自向两侧延伸同色子数。任一方向总长(含中心)≥ <see cref="WinLength"/> 即判胜。
    /// </summary>
    private bool FormsWin(Position last, Stone color)
    {
        // (dRow, dCol):水平、竖直、主对角、反对角
        return RunLength(last, color, 0, 1) >= WinLength
            || RunLength(last, color, 1, 0) >= WinLength
            || RunLength(last, color, 1, 1) >= WinLength
            || RunLength(last, color, 1, -1) >= WinLength;
    }

    private int RunLength(Position center, Stone color, int dRow, int dCol)
    {
        var count = 1; // 中心本身

        // 正方向
        var r = center.Row + dRow;
        var c = center.Col + dCol;
        while (r >= 0 && r < Rows && c >= 0 && c < Cols && _cells[r * Cols + c] == color)
        {
            count++;
            r += dRow;
            c += dCol;
        }

        // 反方向
        r = center.Row - dRow;
        c = center.Col - dCol;
        while (r >= 0 && r < Rows && c >= 0 && c < Cols && _cells[r * Cols + c] == color)
        {
            count++;
            r -= dRow;
            c -= dCol;
        }

        return count;
    }
}
