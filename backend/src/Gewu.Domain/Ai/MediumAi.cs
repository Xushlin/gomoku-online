using Gomoku.Domain.Entities;
using Gomoku.Domain.Enums;
using Gomoku.Domain.ValueObjects;

namespace Gomoku.Domain.Ai;

/// <summary>
/// 中级 AI:三层优先级选点。
/// <list type="number">
/// <item><b>自赢</b>:若存在某空格落己方棋色能立即连五,选该点(多个时随机破平)。</item>
/// <item><b>堵五</b>:若存在某空格落**对手**棋色能立即连五,选该点以阻挡(多个时随机破平)。</item>
/// <item><b>启发分</b>:对所有空格打分 <c>-ChebyshevDistance(p, (7,7)) + (p 的 8 邻域中己方同色子数)</c>,
///     返回分最高者;并列时随机破平。</item>
/// </list>
/// 该 AI 不做博弈树,不识别活四 / 冲四 / 双三。纯函数,不修改入参 <c>board</c>。
/// </summary>
public sealed class MediumAi : IGomokuAi
{
    private const int BoardCenter = 7; // (7,7) 是 15×15 棋盘的几何中心

    private readonly Random _random;

    /// <summary>
    /// 用指定 <see cref="Random"/> 实例构造(用于并列打破)。
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> 为 <c>null</c>。</exception>
    public MediumAi(Random random)
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

        var empties = EnumerateEmpties(board);
        if (empties.Count == 0)
        {
            throw new InvalidOperationException("Cannot select a move on a full board.");
        }

        // 第一层:自赢
        var myWin = myStone == Stone.Black ? GameResult.BlackWin : GameResult.WhiteWin;
        var selfWinning = new List<Position>();
        foreach (var p in empties)
        {
            if (TrialResult(board, p, myStone) == myWin)
            {
                selfWinning.Add(p);
            }
        }
        if (selfWinning.Count > 0)
        {
            return PickOne(selfWinning);
        }

        // 第二层:堵五
        var opponent = myStone == Stone.Black ? Stone.White : Stone.Black;
        var oppWin = opponent == Stone.Black ? GameResult.BlackWin : GameResult.WhiteWin;
        var blocking = new List<Position>();
        foreach (var p in empties)
        {
            if (TrialResult(board, p, opponent) == oppWin)
            {
                blocking.Add(p);
            }
        }
        if (blocking.Count > 0)
        {
            return PickOne(blocking);
        }

        // 第三层:启发分
        var bestScore = int.MinValue;
        var bestMoves = new List<Position>();
        foreach (var p in empties)
        {
            var s = Score(p, myStone, board);
            if (s > bestScore)
            {
                bestScore = s;
                bestMoves.Clear();
                bestMoves.Add(p);
            }
            else if (s == bestScore)
            {
                bestMoves.Add(p);
            }
        }
        return PickOne(bestMoves);
    }

    private static List<Position> EnumerateEmpties(Board board)
    {
        var list = new List<Position>(Position.BoardSize * Position.BoardSize);
        for (var r = 0; r <= Position.MaxIndex; r++)
        {
            for (var c = 0; c <= Position.MaxIndex; c++)
            {
                var p = new Position(r, c);
                if (board.GetStone(p) == Stone.Empty)
                {
                    list.Add(p);
                }
            }
        }
        return list;
    }

    /// <summary>在 <paramref name="board"/> 的副本上试走一子并返回结果;不修改 <paramref name="board"/>。</summary>
    private static GameResult TrialResult(Board board, Position at, Stone stone)
    {
        var clone = board.Clone();
        return clone.PlaceStone(new Move(at, stone));
    }

    private static int Score(Position p, Stone myStone, Board board)
    {
        var centerPenalty = -Math.Max(Math.Abs(p.Row - BoardCenter), Math.Abs(p.Col - BoardCenter));
        var adjacency = 0;
        for (var dr = -1; dr <= 1; dr++)
        {
            for (var dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                var nr = p.Row + dr;
                var nc = p.Col + dc;
                if (nr < 0 || nr > Position.MaxIndex || nc < 0 || nc > Position.MaxIndex) continue;
                if (board.GetStone(new Position(nr, nc)) == myStone)
                {
                    adjacency++;
                }
            }
        }
        return centerPenalty + adjacency;
    }

    private Position PickOne(List<Position> candidates)
    {
        return candidates.Count == 1 ? candidates[0] : candidates[_random.Next(candidates.Count)];
    }
}
