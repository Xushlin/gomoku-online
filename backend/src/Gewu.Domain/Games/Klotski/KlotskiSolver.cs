namespace Gewu.Domain.Games.Klotski;

/// <summary>
/// 华容道求解器 —— A\*,启发函数为曹操左上角到出口的曼哈顿距离。
/// <para>
/// 启发函数**可采纳**:每一步最多让曹操靠近出口一格,所以它永远不高估剩余步数。
/// 因此 A\* 求出的是真正的最优步数,而不是「一个还不错的解」。这一点很重要 ——
/// 关卡产物里的 <c>minMoves</c> 就是它的输出,而计分拿它当分母。
/// </para>
/// <para>
/// **本游戏不引用任何外部数字。** 经典局面的公开步数随数法而异(连滑算一步 vs
/// 一格一步),抄进来既不可复现又可能不自洽 —— 与 <c>add-xiangqi-ai</c> 拒绝声称
/// 「不可战胜」同一条:一个验不了的断言比没有断言更糟。
/// </para>
/// <para>
/// 去重用 <see cref="KlotskiBoard.Signature"/>,它按**形状**而非 id 记格子:
/// 两枚卒交换位置是同一个局面。
/// </para>
/// </summary>
internal static class KlotskiSolver
{
    private static readonly (int Dr, int Dc)[] Directions = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    /// <summary>搜索的展开上限。到顶返回「无解」而不是转圈 —— 见 <see cref="Solve"/>。</summary>
    private const int MaxExpansions = 2_000_000;

    /// <summary>
    /// 求从 <paramref name="board"/> 到「曹操左上角落在 <paramref name="exitRow"/>,
    /// <paramref name="exitCol"/>」的最短走法;无解或盘上没有曹操时返回 <c>null</c>。
    /// </summary>
    /// <param name="board">起始局面。</param>
    /// <param name="exitRow">出口行(曹操左上角)。</param>
    /// <param name="exitCol">出口列。</param>
    internal static IReadOnlyList<KlotskiMove>? Solve(KlotskiBoard board, int exitRow, int exitCol)
    {
        if (board.Target is null)
        {
            return null;
        }
        if (IsSolved(board, exitRow, exitCol))
        {
            return [];
        }

        // came-from 记录「到达这个签名的上一个签名 + 走的那一步」,终点回溯出整条路径。
        var cameFrom = new Dictionary<string, (string Parent, KlotskiMove Move)>(StringComparer.Ordinal);
        var bestCost = new Dictionary<string, int>(StringComparer.Ordinal);
        var boards = new Dictionary<string, KlotskiBoard>(StringComparer.Ordinal);

        var start = board.Signature();
        bestCost[start] = 0;
        boards[start] = board;

        var frontier = new PriorityQueue<string, int>();
        frontier.Enqueue(start, Heuristic(board, exitRow, exitCol));

        var expansions = 0;
        while (frontier.TryDequeue(out var signature, out _))
        {
            if (++expansions > MaxExpansions)
            {
                return null;
            }

            var current = boards[signature];
            var cost = bestCost[signature];

            if (IsSolved(current, exitRow, exitCol))
            {
                return Rebuild(cameFrom, signature);
            }

            for (var index = 0; index < current.Pieces.Count; index++)
            {
                foreach (var (dr, dc) in Directions)
                {
                    var next = current.TryMoveAt(index, dr, dc);
                    if (next is null)
                    {
                        continue;
                    }

                    var nextSignature = next.Signature();
                    var nextCost = cost + 1;
                    if (bestCost.TryGetValue(nextSignature, out var known) && known <= nextCost)
                    {
                        continue;
                    }

                    bestCost[nextSignature] = nextCost;
                    boards[nextSignature] = next;
                    cameFrom[nextSignature] =
                        (signature, new KlotskiMove(current.Pieces[index].Id, dr, dc));
                    frontier.Enqueue(nextSignature, nextCost + Heuristic(next, exitRow, exitCol));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 从当前局面到出口的最短步数;无解返回 <c>null</c>。
    /// <para>
    /// 提示用它做判据:给出的那一步必须让这个数**恰好减一**,否则它不是最短解上的一步。
    /// </para>
    /// </summary>
    /// <param name="board">局面。</param>
    /// <param name="exitRow">出口行。</param>
    /// <param name="exitCol">出口列。</param>
    internal static int? DistanceToGoal(KlotskiBoard board, int exitRow, int exitCol)
        => Solve(board, exitRow, exitCol)?.Count;

    private static bool IsSolved(KlotskiBoard board, int exitRow, int exitCol)
        => board.Target is { } cao && cao.Row == exitRow && cao.Col == exitCol;

    /// <summary>
    /// 曹操左上角到出口的曼哈顿距离。一步最多让它靠近一格,所以永不高估 —— 可采纳。
    /// </summary>
    private static int Heuristic(KlotskiBoard board, int exitRow, int exitCol)
        => board.Target is { } cao
            ? Math.Abs(cao.Row - exitRow) + Math.Abs(cao.Col - exitCol)
            : 0;

    private static List<KlotskiMove> Rebuild(
        Dictionary<string, (string Parent, KlotskiMove Move)> cameFrom, string goal)
    {
        var path = new List<KlotskiMove>();
        var cursor = goal;
        while (cameFrom.TryGetValue(cursor, out var step))
        {
            path.Add(step.Move);
            cursor = step.Parent;
        }
        path.Reverse();
        return path;
    }
}
