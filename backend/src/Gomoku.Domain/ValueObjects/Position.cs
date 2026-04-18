using Gomoku.Domain.Exceptions;

namespace Gomoku.Domain.ValueObjects;

/// <summary>
/// 棋盘坐标值对象。<see cref="Row"/> 与 <see cref="Col"/> 的取值范围均为 [0..14]。
/// 构造时会对范围做严格校验;越界将抛出 <see cref="InvalidMoveException"/>。
/// 不可变且基于值相等(record struct)。
/// </summary>
public readonly record struct Position
{
    /// <summary>棋盘边长(行数与列数均为该值)。</summary>
    public const int BoardSize = 15;

    /// <summary>棋盘上合法坐标的上界(含),即 <c>BoardSize - 1</c>。</summary>
    public const int MaxIndex = BoardSize - 1;

    /// <summary>行索引,范围 [0..14]。</summary>
    public int Row { get; }

    /// <summary>列索引,范围 [0..14]。</summary>
    public int Col { get; }

    /// <summary>
    /// 构造一个棋盘坐标。
    /// </summary>
    /// <param name="row">行索引,必须在 [0..14] 范围内。</param>
    /// <param name="col">列索引,必须在 [0..14] 范围内。</param>
    /// <exception cref="InvalidMoveException">行或列超出 [0..14]。</exception>
    public Position(int row, int col)
    {
        if (row < 0 || row > MaxIndex)
        {
            throw new InvalidMoveException(
                $"Position row {row} is out of board bounds [0..{MaxIndex}].");
        }

        if (col < 0 || col > MaxIndex)
        {
            throw new InvalidMoveException(
                $"Position col {col} is out of board bounds [0..{MaxIndex}].");
        }

        Row = row;
        Col = col;
    }
}
