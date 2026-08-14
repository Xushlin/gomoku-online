using Gewu.Domain.Exceptions;

namespace Gewu.Domain.ValueObjects;

/// <summary>
/// 棋盘坐标值对象。<see cref="Row"/> 与 <see cref="Col"/> 均为**非负**整数。
/// 不可变且基于值相等(record struct)。
/// <para>
/// 这里**只**校验非负 —— 负的行列在任何棋盘上都无意义,所以那条约束属于坐标本身。
/// 上界属于棋种(五子棋 15×15、一字棋 3×3),由 <c>IGameRules.IsInBounds</c> 判定,
/// 并在 <c>Room.PlayMove</c> 触碰棋盘之前执行。
/// </para>
/// <para>
/// 越界仍抛 <see cref="InvalidMoveException"/>,异常类型没变 —— 对外的 HTTP 409 契约
/// 因此不动,变的只是抛出它的那一行。
/// </para>
/// </summary>
public readonly record struct Position
{
    /// <summary>行索引,非负。</summary>
    public int Row { get; }

    /// <summary>列索引,非负。</summary>
    public int Col { get; }

    /// <summary>
    /// 构造一个棋盘坐标。
    /// </summary>
    /// <param name="row">行索引,必须非负。</param>
    /// <param name="col">列索引,必须非负。</param>
    /// <exception cref="InvalidMoveException">行或列为负。</exception>
    public Position(int row, int col)
    {
        if (row < 0)
        {
            throw new InvalidMoveException($"Position row {row} must not be negative.");
        }

        if (col < 0)
        {
            throw new InvalidMoveException($"Position col {col} must not be negative.");
        }

        Row = row;
        Col = col;
    }
}
