using Gomoku.Domain.Enums;
using Gomoku.Domain.Exceptions;

namespace Gomoku.Domain.ValueObjects;

/// <summary>
/// 一次落子:在 <see cref="Position"/> 上落下一枚 <see cref="Stone"/>。
/// 不可变值对象;<see cref="Stone"/> 不可为 <see cref="Enums.Stone.Empty"/>。
/// </summary>
public readonly record struct Move
{
    /// <summary>落子位置。</summary>
    public Position Position { get; }

    /// <summary>落下的棋色。</summary>
    public Stone Stone { get; }

    /// <summary>
    /// 构造一次落子。
    /// </summary>
    /// <param name="position">落子位置。</param>
    /// <param name="stone">落子棋色,不能为 <see cref="Enums.Stone.Empty"/>。</param>
    /// <exception cref="InvalidMoveException"><paramref name="stone"/> 为 <see cref="Enums.Stone.Empty"/>。</exception>
    public Move(Position position, Stone stone)
    {
        if (stone == Stone.Empty)
        {
            throw new InvalidMoveException(
                "Move stone cannot be Stone.Empty; use Black or White.");
        }

        Position = position;
        Stone = stone;
    }
}
