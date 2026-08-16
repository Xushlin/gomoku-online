using Gewu.Domain.Ai;
using Gewu.Domain.Entities;
using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;
using DomainMove = Gewu.Domain.ValueObjects.Move;

namespace Gewu.Domain.Games.TicTacToe;

/// <summary>
/// 一字棋的中级 AI:自赢 → 堵对手 → 中心 → 角 → 随机。
/// <para>
/// 它**故意可以被击败** —— 这是难度阶梯里唯一"会犯错但不犯低级错"的档位。
/// <see cref="TicTacToeHardAi"/> 不可战胜,<see cref="EasyAi"/> 毫无抵抗;三档之间的区别
/// 必须是玩家能观察到的,否则难度选择器只是个装饰。
/// </para>
/// <para>
/// 它会怎么输:只看一步。对手做出双威胁(一手同时造出两条各差一子的线)时,它只能堵掉
/// 其中一条。这不是需要修的缺陷,正是"中级"的含义。
/// </para>
/// <para>
/// 不复用五子棋的 <c>MediumAi</c>:那个实现的中心是写死的 <c>(7,7)</c>,启发分按连子长度
/// 打分 —— 在 3×3 上"连子长度"只有 1、2、3 三档,而 3 已经是终局。它需要的判断("这一手
/// 会不会给对手造出双威胁")在五子棋的评分语言里根本不存在。
/// </para>
/// </summary>
public sealed class TicTacToeMediumAi : IPlacementAi
{
    private readonly Random _random;

    /// <summary>用指定 <see cref="Random"/> 构造,仅用于并列打破。</summary>
    /// <param name="random">随机源。</param>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> 为 <c>null</c>。</exception>
    public TicTacToeMediumAi(Random random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <inheritdoc />
    public Position SelectMove(Board board, Stone myStone)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (myStone == Stone.Empty)
        {
            throw new ArgumentOutOfRangeException(
                nameof(myStone), myStone, "Bot stone must be Black or White, not Empty.");
        }

        var empties = TicTacToeBoard.EmptyCells(board);
        if (empties.Count == 0)
        {
            throw new InvalidOperationException("Cannot select a move on a full board.");
        }

        var opponent = myStone == Stone.Black ? Stone.White : Stone.Black;

        // ① 有一手能立即赢就走它。
        if (TicTacToeBoard.FindWinningMove(board, empties, myStone) is { } win)
        {
            return win;
        }

        // ② 否则堵掉对手的立即取胜点。顺序不能反 —— 自己能赢的时候去堵,是把胜势让掉。
        if (TicTacToeBoard.FindWinningMove(board, empties, opponent) is { } block)
        {
            return block;
        }

        // ③ 中心。3×3 上中心参与四条线,是单格价值最高的位置。
        var centre = new Position(1, 1);
        if (board.GetStone(centre) == Stone.Empty)
        {
            return centre;
        }

        // ④ 角。每个角参与三条线,边只参与两条。
        var corners = empties.Where(TicTacToeBoard.IsCorner).ToList();
        if (corners.Count > 0)
        {
            return corners[_random.Next(corners.Count)];
        }

        // ⑤ 剩下的边,随机。
        return empties[_random.Next(empties.Count)];
    }
}
