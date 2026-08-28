using Gewu.Domain.Ai;
using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.Xiangqi;

/// <summary>
/// 中国象棋 AI：限深 alpha-beta + 子力/位置评估。
/// <para>
/// **不声称任何一档「不可战胜」。** 象棋的状态空间不可能穷举，与一字棋 Hard 档那套
/// 穷举 minimax 是两回事 —— 那里可以断言「落在博弈论最优值上」，这里不能，
/// 而一个验不了的断言比没有断言更糟。可验证的是：着法合法、看得见一步吃子、
/// 以及深一档不弱于浅一档。
/// </para>
/// <para>
/// 无状态、纯函数：同一段历史 + 同一随机源 → 同一着法。
/// </para>
/// </summary>
public sealed class XiangqiAi : IBoardGameAi
{
    /// <summary>子力价值。将帅给一个远大于其余总和的数，于是「丢将」永远压倒任何得子。</summary>
    private static readonly Dictionary<XiangqiPieceType, int> PieceValue = new()
    {
        [XiangqiPieceType.General] = 100000,
        [XiangqiPieceType.Chariot] = 900,
        [XiangqiPieceType.Cannon] = 450,
        [XiangqiPieceType.Horse] = 400,
        [XiangqiPieceType.Advisor] = 200,
        [XiangqiPieceType.Elephant] = 200,
        [XiangqiPieceType.Soldier] = 100,
    };

    private readonly XiangqiRules _rules;
    private readonly Random _random;
    private readonly int _depth;

    /// <summary>
    /// 构造一个指定搜索深度的象棋 AI。
    /// </summary>
    /// <param name="rules">规则 —— 着法枚举走它，AI 不自己实现一遍。</param>
    /// <param name="random">随机源，用于打破同分并列。</param>
    /// <param name="depth">搜索层数；<c>0</c> 表示只看当前一步的吃子价值。</param>
    public XiangqiAi(XiangqiRules rules, Random random, int depth)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(random);
        _rules = rules;
        _random = random;
        _depth = depth;
    }

    /// <inheritdoc />
    public MoveIntent SelectMove(IReadOnlyList<PlayedMove> history, Stone myStone)
    {
        if (myStone == Stone.Empty)
        {
            throw new ArgumentOutOfRangeException(
                nameof(myStone), myStone, "AI side cannot be Stone.Empty.");
        }

        // 着法枚举走规则 —— AI 自己再实现一遍就是第二份真源，而不一致的表现是
        // 「机器人走出规则会拒绝的棋」，用户看到的是它卡住了。
        var moves = _rules.LegalMoves(history, myStone);
        if (moves.Count == 0)
        {
            // 此前这句写的是「the game should already have ended」。长将上限落地之后那不再是
            // 唯一的解释:一方的每一条着法都可以被上限挡住,而那时棋确实还没结束 ——
            // 收场的是回合超时。一句断言了错误原因的报错,会把下一个人送错方向。
            throw new InvalidOperationException(
                $"{myStone} has no permitted move: checkmate, stalemate, or every move is a "
                + "repeated check past its limit.");
        }

        var board = XiangqiRules.BoardFrom(history);

        var best = int.MinValue;
        var bestMoves = new List<MoveIntent>();
        foreach (var move in moves)
        {
            var after = board.Clone();
            after.Move(move.From!.Value, move.To!.Value);
            var score = -Search(after, Opponent(myStone), _depth, int.MinValue + 1, int.MaxValue - 1);

            if (score > best)
            {
                best = score;
                bestMoves.Clear();
                bestMoves.Add(move);
            }
            else if (score == best)
            {
                bestMoves.Add(move);
            }
        }

        // 同分并列时随机挑一个，否则 AI 每局都下一模一样的棋。
        return bestMoves[_random.Next(bestMoves.Count)];
    }

    private static Stone Opponent(Stone side) => side == Stone.Black ? Stone.White : Stone.Black;

    /// <summary>Negamax + alpha-beta。返回**轮到 <paramref name="side"/> 走**时该局面对它的分数。</summary>
    private int Search(XiangqiBoard board, Stone side, int depth, int alpha, int beta)
    {
        if (depth <= 0)
        {
            return Evaluate(board, side);
        }

        var moves = PseudoLegalMovesFor(board, side);
        if (moves.Count == 0)
        {
            // 无着可走 = 将死或困毙，两者都判负。给一个比任何子力差都大的负分。
            return -PieceValue[XiangqiPieceType.General];
        }

        var best = int.MinValue + 1;
        foreach (var move in Ordered(board, moves))
        {
            var after = board.Clone();
            after.Move(move.From!.Value, move.To!.Value);
            var score = -Search(after, Opponent(side), depth - 1, -beta, -alpha);
            if (score > best)
            {
                best = score;
            }
            if (best > alpha)
            {
                alpha = best;
            }
            if (alpha >= beta)
            {
                break;  // 剪枝：对手不会让局面走到这里。
            }
        }
        return best;
    }

    /// <summary>
    /// 搜索内部用的着法枚举 —— 只过滤「走完自将」，与 <c>XiangqiRules</c> 的判定同一套。
    /// <para>
    /// 这里不复用 <c>LegalMoves(history, side)</c>，是因为那个入口收的是**历史**：
    /// 搜索每往下一层都得把历史重放一遍，复杂度会从 O(b^d) 变成 O(b^d · n)。
    /// 判定逻辑仍然只有一份 —— 它们调的是同一个 <c>XiangqiRules</c> 内部方法。
    /// </para>
    /// </summary>
    private static List<MoveIntent> PseudoLegalMovesFor(XiangqiBoard board, Stone side)
        => XiangqiRules.LegalMovesOnBoard(board, side);

    /// <summary>
    /// 先搜吃子，吃大子的排最前。
    /// <para>
    /// alpha-beta 的剪枝量**完全取决于顺序**：先看到好着法，后面的分支才会被大量砍掉。
    /// 不排序时深度 3 一步要两秒 —— 那不只是测试慢，是机器人在真实对局里会卡住。
    /// 排完之后同样的深度快一个数量级，而结果一模一样（剪掉的分支按定义都是不影响结论的）。
    /// </para>
    /// </summary>
    private static IEnumerable<MoveIntent> Ordered(XiangqiBoard board, List<MoveIntent> moves)
        => moves.OrderByDescending(m =>
            board.At(m.To!.Value) is { } victim ? PieceValue[victim.Type] : 0);

    /// <summary>从 <paramref name="side"/> 的视角给局面打分：己方子力减对方子力。</summary>
    private static int Evaluate(XiangqiBoard board, Stone side)
    {
        var score = 0;
        for (var row = 0; row < XiangqiBoard.RowCount; row++)
        {
            for (var col = 0; col < XiangqiBoard.ColCount; col++)
            {
                if (board.At(row, col) is not { } piece)
                {
                    continue;
                }

                var value = PieceValue[piece.Type] + PositionBonus(piece, row);
                score += piece.Side == side ? value : -value;
            }
        }
        return score;
    }

    /// <summary>
    /// 位置项，只做一件事：**鼓励兵过河**。
    /// <para>
    /// 过河的兵能横走，价值明显高于未过河的。除此之外不加更多位置项 ——
    /// 没有 UI、没人能感受到差别之前，多调一个系数只是没有依据的手工调参。
    /// </para>
    /// </summary>
    private static int PositionBonus(XiangqiPiece piece, int row)
    {
        if (piece.Type != XiangqiPieceType.Soldier)
        {
            return 0;
        }
        var crossed = piece.Side == Stone.Black ? row <= 4 : row >= 5;
        return crossed ? 80 : 0;
    }
}
