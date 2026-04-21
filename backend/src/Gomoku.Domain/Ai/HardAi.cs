using Gomoku.Domain.Entities;
using Gomoku.Domain.Enums;
using Gomoku.Domain.ValueObjects;
using Move = Gomoku.Domain.ValueObjects.Move;

namespace Gomoku.Domain.Ai;

/// <summary>
/// 高级 AI:Minimax + α-β 两层前瞻搜索(默认 <c>searchDepth=2</c>),配合一个基于
/// "同色连续段 × 封闭度"的启发式评估函数。不做 VCF/VCT、迭代深化、transposition —— 目标是
/// "比 Medium 明显强,认真下才能赢",不是顶级。
/// <para>
/// 关键剪枝:候选生成 <see cref="GenerateCandidates"/> 把搜索空间从 ~225 降到"已有子周围 5×5
/// 方块内的空格"(典型 10–30 个)。配合 α-β,两层搜索单步 &lt; 10ms。纯函数 / 不修改入参。
/// </para>
/// </summary>
public sealed class HardAi : IGomokuAi
{
    private const int BoardCenter = 7;
    private const int NeighbourRadius = 2;

    // 评估分数(design D3 的权重表)。对手模式会乘 OppWeightMultiplier 以偏防守。
    private const int ScoreOpenFour = 10_000;
    private const int ScoreClosedFour = 1_000;
    private const int ScoreOpenThree = 500;
    private const int ScoreClosedThree = 100;
    private const int ScoreOpenTwo = 50;
    private const int ScoreClosedTwo = 10;
    private const int TerminalWin = 100_000;

    private readonly Random _random;
    private readonly int _searchDepth;

    /// <summary>
    /// 用指定 <see cref="Random"/> 与搜索深度构造 HardAi。默认 <paramref name="searchDepth"/>
    /// 为 2;`GomokuAiFactory` 只会用默认值,非默认仅供测试用。
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> 为 null。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="searchDepth"/> &lt; 1。</exception>
    public HardAi(Random random, int searchDepth = 2)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
        if (searchDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(searchDepth), searchDepth, "Search depth must be >= 1.");
        }
        _searchDepth = searchDepth;
    }

    /// <inheritdoc />
    public Position SelectMove(Board board, Stone myStone)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (myStone == Stone.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(myStone), myStone, "Bot stone must be Black or White, not Empty.");
        }

        // 空盘首手:直接中心
        if (IsBoardEmpty(board))
        {
            return new Position(BoardCenter, BoardCenter);
        }

        var oppStone = myStone == Stone.Black ? Stone.White : Stone.Black;
        var candidates = GenerateCandidates(board);
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("Cannot select a move on a full board.");
        }

        // 自赢剪枝 + Minimax 搜索
        var bestScore = int.MinValue;
        var bestMoves = new List<Position>();
        foreach (var c in candidates)
        {
            var cloned = board.Clone();
            var result = cloned.PlaceStone(new Move(c, myStone));

            // 己方走这一步即连五 → 直接选
            if (IsWinForStone(result, myStone))
            {
                return c;
            }

            int score;
            if (result != GameResult.Ongoing)
            {
                // 平局 / 对方胜(不可能,因为这是自己走)—— 按评估函数处理
                score = Evaluate(cloned, myStone);
            }
            else
            {
                // 对方先行(minimizing)搜索 depth - 1 层
                score = Minimax(
                    cloned,
                    depth: _searchDepth - 1,
                    isMaximizing: false,
                    alpha: int.MinValue,
                    beta: int.MaxValue,
                    myStone: myStone,
                    oppStone: oppStone);
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMoves.Clear();
                bestMoves.Add(c);
            }
            else if (score == bestScore)
            {
                bestMoves.Add(c);
            }
        }

        return bestMoves.Count == 1 ? bestMoves[0] : bestMoves[_random.Next(bestMoves.Count)];
    }

    // =========================================================================
    // Minimax
    // =========================================================================

    private int Minimax(Board board, int depth, bool isMaximizing, int alpha, int beta, Stone myStone, Stone oppStone)
    {
        if (depth == 0)
        {
            return Evaluate(board, myStone);
        }

        var stoneToPlay = isMaximizing ? myStone : oppStone;
        var candidates = GenerateCandidates(board);
        if (candidates.Count == 0)
        {
            return Evaluate(board, myStone);
        }

        if (isMaximizing)
        {
            var bestScore = int.MinValue;
            foreach (var c in candidates)
            {
                var cloned = board.Clone();
                var result = cloned.PlaceStone(new Move(c, stoneToPlay));

                int score;
                if (IsWinForStone(result, myStone))
                {
                    score = TerminalWin;
                }
                else if (IsWinForStone(result, oppStone))
                {
                    // 不可能 —— maximizing 分支走的是 myStone;防御式
                    score = -TerminalWin;
                }
                else if (result != GameResult.Ongoing)
                {
                    score = Evaluate(cloned, myStone); // 平局
                }
                else
                {
                    score = Minimax(cloned, depth - 1, isMaximizing: false, alpha, beta, myStone, oppStone);
                }

                if (score > bestScore) bestScore = score;
                if (bestScore > alpha) alpha = bestScore;
                if (alpha >= beta) break; // α-β 剪枝
            }
            return bestScore;
        }
        else
        {
            var bestScore = int.MaxValue;
            foreach (var c in candidates)
            {
                var cloned = board.Clone();
                var result = cloned.PlaceStone(new Move(c, stoneToPlay));

                int score;
                if (IsWinForStone(result, oppStone))
                {
                    score = -TerminalWin;
                }
                else if (IsWinForStone(result, myStone))
                {
                    score = TerminalWin;
                }
                else if (result != GameResult.Ongoing)
                {
                    score = Evaluate(cloned, myStone);
                }
                else
                {
                    score = Minimax(cloned, depth - 1, isMaximizing: true, alpha, beta, myStone, oppStone);
                }

                if (score < bestScore) bestScore = score;
                if (bestScore < beta) beta = bestScore;
                if (alpha >= beta) break;
            }
            return bestScore;
        }
    }

    // =========================================================================
    // Candidate generation
    // =========================================================================

    internal static List<Position> GenerateCandidates(Board board)
    {
        // 先标记所有需要考察的空格(距离任一已有子 ≤ NeighbourRadius)
        var seen = new bool[Position.BoardSize, Position.BoardSize];
        var result = new List<Position>();

        for (var r = 0; r <= Position.MaxIndex; r++)
        {
            for (var c = 0; c <= Position.MaxIndex; c++)
            {
                if (board.GetStone(new Position(r, c)) == Stone.Empty) continue;

                for (var dr = -NeighbourRadius; dr <= NeighbourRadius; dr++)
                {
                    for (var dc = -NeighbourRadius; dc <= NeighbourRadius; dc++)
                    {
                        var nr = r + dr;
                        var nc = c + dc;
                        if (nr < 0 || nr > Position.MaxIndex || nc < 0 || nc > Position.MaxIndex) continue;
                        if (seen[nr, nc]) continue;
                        var p = new Position(nr, nc);
                        if (board.GetStone(p) == Stone.Empty)
                        {
                            seen[nr, nc] = true;
                            result.Add(p);
                        }
                    }
                }
            }
        }

        return result;
    }

    private static bool IsBoardEmpty(Board board)
    {
        for (var r = 0; r <= Position.MaxIndex; r++)
        {
            for (var c = 0; c <= Position.MaxIndex; c++)
            {
                if (board.GetStone(new Position(r, c)) != Stone.Empty) return false;
            }
        }
        return true;
    }

    private static bool IsWinForStone(GameResult result, Stone stone) =>
        (stone == Stone.Black && result == GameResult.BlackWin) ||
        (stone == Stone.White && result == GameResult.WhiteWin);

    // =========================================================================
    // Evaluation:沿 4 方向扫描同色连续段,按模式打分
    // =========================================================================

    internal static int Evaluate(Board board, Stone myStone)
    {
        var oppStone = myStone == Stone.Black ? Stone.White : Stone.Black;
        // 同色连续段沿 4 方向去重扫描;`visited[dir, r, c]` 记录该方向下该格是否已参与某段的起点扫描
        var my = ScoreAllSegments(board, myStone);
        var opp = ScoreAllSegments(board, oppStone);
        // 对手权重乘 1.1(偏防守);注意 opp 已经是正分,要反号
        return my - (int)Math.Round(opp * 1.1);
    }

    /// <summary>
    /// 沿 4 方向扫描 <paramref name="board"/> 上所有长度 ≥ 2 的 <paramref name="stone"/> 同色连续段,
    /// 按模式 × 封闭度累加分数。返回非负整数。
    /// </summary>
    private static int ScoreAllSegments(Board board, Stone stone)
    {
        var total = 0;
        // 4 方向:(dr, dc) = (0,1) 水平、(1,0) 垂直、(1,1) 主对角 ↘、(1,-1) 反对角 ↗
        int[,] dirs = { { 0, 1 }, { 1, 0 }, { 1, 1 }, { 1, -1 } };

        for (var d = 0; d < 4; d++)
        {
            var dr = dirs[d, 0];
            var dc = dirs[d, 1];
            for (var r = 0; r <= Position.MaxIndex; r++)
            {
                for (var c = 0; c <= Position.MaxIndex; c++)
                {
                    if (board.GetStone(new Position(r, c)) != stone) continue;

                    // 只从"段起点"开始扫(前一格不是同色),避免重复计数
                    var pr = r - dr;
                    var pc = c - dc;
                    if (pr >= 0 && pr <= Position.MaxIndex && pc >= 0 && pc <= Position.MaxIndex
                        && board.GetStone(new Position(pr, pc)) == stone)
                    {
                        continue;
                    }

                    // 从 (r, c) 沿方向扫段
                    var len = 0;
                    var nr = r;
                    var nc = c;
                    while (nr >= 0 && nr <= Position.MaxIndex && nc >= 0 && nc <= Position.MaxIndex
                           && board.GetStone(new Position(nr, nc)) == stone)
                    {
                        len++;
                        nr += dr;
                        nc += dc;
                    }

                    if (len < 2) continue; // 单子不计分

                    // 两端状态
                    var leftOpen = IsOpenEnd(board, pr, pc, stone);
                    var rightOpen = IsOpenEnd(board, nr, nc, stone);
                    var openEnds = (leftOpen ? 1 : 0) + (rightOpen ? 1 : 0);

                    total += ScoreFor(len, openEnds);
                }
            }
        }

        return total;
    }

    private static bool IsOpenEnd(Board board, int r, int c, Stone selfStone)
    {
        // 越界视为封闭
        if (r < 0 || r > Position.MaxIndex || c < 0 || c > Position.MaxIndex) return false;
        // 空格视为活
        var s = board.GetStone(new Position(r, c));
        return s == Stone.Empty;
    }

    private static int ScoreFor(int length, int openEnds)
    {
        // 长度 ≥ 5 不应出现(由 Minimax 终局短路捕获),此处为防御
        if (length >= 5) return TerminalWin;

        return (length, openEnds) switch
        {
            (4, 2) => ScoreOpenFour,
            (4, 1) => ScoreClosedFour,
            (4, 0) => 0, // 两端封死的 4 连无威胁
            (3, 2) => ScoreOpenThree,
            (3, 1) => ScoreClosedThree,
            (3, 0) => 0,
            (2, 2) => ScoreOpenTwo,
            (2, 1) => ScoreClosedTwo,
            (2, 0) => 0,
            _ => 0,
        };
    }
}
