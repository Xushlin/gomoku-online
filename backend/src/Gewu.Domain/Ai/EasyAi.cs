using Gomoku.Domain.Entities;
using Gomoku.Domain.Enums;
using Gomoku.Domain.ValueObjects;

namespace Gomoku.Domain.Ai;

/// <summary>
/// 初级 AI:在棋盘所有空格里**均匀随机**挑一个。无任何策略。
/// 所有随机性 MUST 通过构造时注入的 <see cref="Random"/> 产生;不得隐式 <c>new Random()</c>,
/// 以保证测试可用固定种子复现。
/// </summary>
public sealed class EasyAi : IGomokuAi
{
    private readonly Random _random;

    /// <summary>
    /// 用指定 <see cref="Random"/> 实例构造 AI。测试中传入固定种子以得到确定行为;
    /// 生产中由 <c>IAiRandomProvider</c> 注入 <see cref="Random.Shared"/> 的包装。
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> 为 <c>null</c>。</exception>
    public EasyAi(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <inheritdoc />
    public Position SelectMove(Board board, Stone myStone)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (myStone == Stone.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(myStone), myStone, "Bot stone must be Black or White, not Empty.");
        }

        var empties = new List<Position>(Position.BoardSize * Position.BoardSize);
        for (var r = 0; r <= Position.MaxIndex; r++)
        {
            for (var c = 0; c <= Position.MaxIndex; c++)
            {
                var p = new Position(r, c);
                if (board.GetStone(p) == Stone.Empty)
                {
                    empties.Add(p);
                }
            }
        }

        if (empties.Count == 0)
        {
            throw new InvalidOperationException("Cannot select a move on a full board.");
        }

        return empties[_random.Next(empties.Count)];
    }
}
