using Gewu.Domain.Ai;
using Gewu.Domain.Entities;
using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;
using DomainMove = Gewu.Domain.ValueObjects.Move;

namespace Gewu.Domain.Games.TicTacToe;

/// <summary>
/// 一字棋的高级 AI:穷举整棵博弈树的 minimax。
/// <para>
/// **没有评估函数、没有深度截断、没有棋形启发。** 3×3 的可达局面只有 5,478 个,
/// 完整搜索是瞬时的 —— 所以完美走法是**完备性**的推论,不是调参的产物。这是它与五子棋
/// <c>HardAi</c> 的根本区别:后者搜两层然后猜,只能"看起来很强";本类不猜。
/// </para>
/// <para>
/// 由此得到一条比任何启发式 AI 都强的可测性质:**它永远不会输**。这条性质可以对着整棵树
/// 穷举验证(见 <c>TicTacToeHardAiTests</c>),而不是抽样几个局面看看像不像话。
/// 选一个已解游戏当第二个棋种,这就是回报之一。
/// </para>
/// <para>
/// 已知后果:玩家打不赢它,最好的结果是和棋。那是一字棋这个游戏的事实,不是缺陷。
/// Easy 与 Medium 仍可战胜。
/// </para>
/// <para>
/// 确定性:不用随机源,同一局面永远给同一手。并列的最优手里随机挑一个也不会输,但确定性
/// 让"它永远不会输"从抽样断言变成穷举断言 —— 可证 &gt; 好看。
/// </para>
/// </summary>
public sealed class TicTacToeHardAi : IPlacementAi
{
    /// <summary>胜负分基准。大于最大深度(9),所以"赢"永远压过"和",符号不会串。</summary>
    private const int WinBase = 100;

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

        // 备忘表是**每次调用的局部变量**,不是字段也不是静态 —— IBoardGameAi 要求实现
        // 不读写可变共享状态。它把重复局面(同一盘面可由不同着法顺序到达)折叠掉,
        // 把 9! 条路径降到 5,478 个局面,于是"穷举"真的可以每步都跑一遍。
        var memo = new Dictionary<(int State, Stone ToMove), int>();

        var best = empties[0];
        var bestScore = int.MinValue;

        foreach (var p in empties)
        {
            var trial = board.Clone();
            var result = trial.PlaceStone(new DomainMove(p, myStone));
            var score = result == GameResult.Ongoing
                ? Search(trial, myStone, Opponent(myStone), 1, memo)
                : TerminalScore(result, myStone, 1);

            // 严格大于:并列时保留阅读顺序靠前的那一手,保证确定性。
            if (score > bestScore)
            {
                bestScore = score;
                best = p;
            }
        }

        return best;
    }

    /// <summary>
    /// 返回 <paramref name="myStone"/> 视角下本局面的分数,假定双方此后都走最优。
    /// </summary>
    private static int Search(
        Board board,
        Stone myStone,
        Stone toMove,
        int depth,
        Dictionary<(int, Stone), int> memo)
    {
        var state = Encode(board);
        if (memo.TryGetValue((state, toMove), out var cached))
        {
            return cached;
        }

        var maximising = toMove == myStone;
        var best = maximising ? int.MinValue : int.MaxValue;

        // 调用方只在 Ongoing 上递归,而满盘不可能是 Ongoing(判胜会给出 Draw),
        // 所以这里的空格集合必然非空。
        foreach (var p in TicTacToeBoard.EmptyCells(board))
        {
            var trial = board.Clone();
            var result = trial.PlaceStone(new DomainMove(p, toMove));
            var score = result == GameResult.Ongoing
                ? Search(trial, myStone, Opponent(toMove), depth + 1, memo)
                : TerminalScore(result, myStone, depth + 1);

            best = maximising ? Math.Max(best, score) : Math.Min(best, score);
        }

        memo[(state, toMove)] = best;
        return best;
    }

    /// <summary>
    /// 终局分。深度参与计分,所以"能赢就早点赢、要输就晚点输" ——
    /// 这也顺带保证了"有立即取胜的一手就走它":同样是赢,浅的分更高。
    /// </summary>
    private static int TerminalScore(GameResult result, Stone myStone, int depth)
    {
        if (result == GameResult.Draw)
        {
            return 0;
        }
        return TicTacToeBoard.IsWinFor(result, myStone) ? WinBase - depth : depth - WinBase;
    }

    /// <summary>把盘面编码成一个三进制整数,用作备忘表的键。</summary>
    private static int Encode(Board board)
    {
        var key = 0;
        for (var r = 0; r < board.Rows; r++)
        {
            for (var c = 0; c < board.Cols; c++)
            {
                key = (key * 3) + (int)board.GetStone(new Position(r, c));
            }
        }
        return key;
    }

    private static Stone Opponent(Stone stone) => stone == Stone.Black ? Stone.White : Stone.Black;
}
