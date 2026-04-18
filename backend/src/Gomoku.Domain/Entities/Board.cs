using Gomoku.Domain.Enums;
using Gomoku.Domain.Exceptions;
using Gomoku.Domain.ValueObjects;

namespace Gomoku.Domain.Entities;

/// <summary>
/// 15×15 五子棋棋盘聚合实体。维护棋格状态,提供落子、查询、判胜、克隆与重置。
/// 判胜采用"只检查刚落子"的增量算法,单次 <see cref="PlaceStone"/> 为 O(1)。
/// 非线程安全 —— 约定在单线程下使用(一盘对局一个实例)。
/// <para>
/// 调用方(Application、AI、SignalR Hub)应在调用 <see cref="PlaceStone"/> 之前自行校验落点合法性。
/// 本类抛出的 <see cref="InvalidMoveException"/> 仅用于保护 Domain 不变量,
/// 不应作为"某位置是否能落子"的查询手段。
/// </para>
/// </summary>
public sealed class Board
{
    private const int Size = Position.BoardSize;
    private const int WinLength = 5;
    private const int CellCount = Size * Size;

    private readonly Stone[] _cells;

    /// <summary>构造一个空棋盘,所有格子均为 <see cref="Stone.Empty"/>。</summary>
    public Board()
    {
        _cells = new Stone[CellCount];
    }

    /// <summary>查询指定位置的棋子。</summary>
    /// <param name="position">合法棋盘坐标。</param>
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
    /// <exception cref="InvalidMoveException">目标格已有棋子。棋盘状态保持不变。</exception>
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
            return move.Stone == Stone.Black ? GameResult.BlackWin : GameResult.WhiteWin;
        }

        return IsFull() ? GameResult.Draw : GameResult.Ongoing;
    }

    /// <summary>返回一份完全独立的棋盘副本,供 AI 搜索等"试走"场景使用。</summary>
    public Board Clone()
    {
        var clone = new Board();
        Array.Copy(_cells, clone._cells, CellCount);
        return clone;
    }

    /// <summary>把棋盘恢复为初始空盘。</summary>
    public void Reset()
    {
        Array.Clear(_cells);
    }

    private static int IndexOf(Position position) => position.Row * Size + position.Col;

    private bool IsFull()
    {
        for (var i = 0; i < CellCount; i++)
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
        while (r >= 0 && r < Size && c >= 0 && c < Size && _cells[r * Size + c] == color)
        {
            count++;
            r += dRow;
            c += dCol;
        }

        // 反方向
        r = center.Row - dRow;
        c = center.Col - dCol;
        while (r >= 0 && r < Size && c >= 0 && c < Size && _cells[r * Size + c] == color)
        {
            count++;
            r -= dRow;
            c -= dCol;
        }

        return count;
    }
}
