using Gewu.Domain.Ai;
using Gewu.Domain.Entities;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.TicTacToe;
using Gewu.Domain.ValueObjects;
using DomainMove = Gewu.Domain.ValueObjects.Move;

namespace Gewu.Domain.Tests.Games.TicTacToe;

/// <summary>
/// 一字棋 Hard 档的**穷举**验证。
/// <para>
/// 这组测试是选一字棋当第二个棋种的主要回报。五子棋的 <c>HardAi</c> 只能被抽样检查
/// ("这几个局面它走得像不像话"),因为它搜两层然后靠评估函数猜 —— 没有任何断言能说
/// 它"下得对"。一字棋是**已解游戏**,所以"它永远不会输"是一条可以对着整棵博弈树穷举
/// 证明的性质,而不是一个感觉。
/// </para>
/// </summary>
public class TicTacToeHardAiTests
{
    private static readonly INInARowRules Rules = BuiltInGameRules.TicTacToe;

    private static Stone Other(Stone s) => s == Stone.Black ? Stone.White : Stone.Black;

    /// <summary>Hard 执 <paramref name="botStone"/>,对手穷举所有合法应手。收集所有终局。</summary>
    private static void PlayOutEveryLine(
        Board board, Stone botStone, Stone toMove, List<GameResult> outcomes)
    {
        var ai = new TicTacToeHardAi();

        if (toMove == botStone)
        {
            var pick = ai.SelectMove(board, botStone);

            // 契约:落在空格上,且不改动入参。
            board.GetStone(pick).Should().Be(Stone.Empty);

            var next = board.Clone();
            var result = next.PlaceStone(new DomainMove(pick, botStone));
            if (result != GameResult.Ongoing)
            {
                outcomes.Add(result);
                return;
            }
            PlayOutEveryLine(next, botStone, Other(botStone), outcomes);
            return;
        }

        // 对手:每一个空格都试一遍 —— 这才叫"穷举所有合法应手"。
        foreach (var p in AllEmpties(board))
        {
            var next = board.Clone();
            var result = next.PlaceStone(new DomainMove(p, toMove));
            if (result != GameResult.Ongoing)
            {
                outcomes.Add(result);
                continue;
            }
            PlayOutEveryLine(next, botStone, botStone, outcomes);
        }
    }

    private static List<Position> AllEmpties(Board board)
    {
        var list = new List<Position>();
        for (var r = 0; r < board.Rows; r++)
        {
            for (var c = 0; c < board.Cols; c++)
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

    private static bool IsLossFor(GameResult result, Stone stone)
        => stone == Stone.Black ? result == GameResult.WhiteWin : result == GameResult.BlackWin;

    // ---- 本变更的核心断言 ----

    [Theory]
    [InlineData(Stone.Black)] // 先手
    [InlineData(Stone.White)] // 后手
    public void Hard_never_loses_from_the_opening_whichever_side_it_plays(Stone botStone)
    {
        // Black 先走。bot 执白时,对手先动。
        var outcomes = new List<GameResult>();

        PlayOutEveryLine(Rules.CreateBoard(), botStone, Stone.Black, outcomes);

        outcomes.Should().NotBeEmpty();
        outcomes.Should().NotContain(r => IsLossFor(r, botStone));
        outcomes.Should().OnlyContain(r => r != GameResult.Ongoing);
    }

    [Fact]
    public void Hard_achieves_the_game_theoretic_result_from_every_legal_position()
    {
        // 比上一条更强,但断言的**不是**"永不落败" —— 那句话对任意局面是假的。
        // 反例:
        //     X O X
        //     O X .        X 有 (0,0)(1,1) 与 (0,2)(1,1) 两条各差一子的线 —— 双威胁。
        //     . . .        轮到 O,堵哪边都输。这是个合法局面,但在 Hard 接手之前就已经输定了。
        //
        // 完美走法保证不了的是"从死局里翻盘";它保证的是"永远拿到这个局面本来能拿到的
        // 最好结果"。所以这里用一个独立写的 negamax 求出每个局面的理论值,再要求 Hard
        // 的最坏结果**正好等于**它 —— 不是"不差于",是"相等":拿到比理论值更好的结果
        // 意味着求值器错了,同样该失败。
        var boards = new List<(Board Board, Stone ToMove)>();
        Enumerate(Rules.CreateBoard(), Stone.Black, [], boards);

        // 3×3 的可达非终局局面 —— 这个数字就是"穷举整棵树是可行的"那句话的依据。
        boards.Should().HaveCountGreaterThan(500);

        var memo = new Dictionary<(int, Stone), int>();

        foreach (var (board, toMove) in boards)
        {
            var theoretical = Value(board, toMove, memo);

            var outcomes = new List<GameResult>();
            PlayOutEveryLine(board, toMove, toMove, outcomes);
            var worst = outcomes.Min(r => Signed(r, toMove));

            worst.Should().Be(
                theoretical,
                $"Hard playing {toMove} from {Describe(board)} must land on the game-theoretic value");
        }
    }

    /// <summary>
    /// 局面在 <paramref name="toMove"/> 视角下的理论值:+1 胜、0 和、-1 负。
    /// <para>
    /// 刻意用 negamax(±1、无深度分、无最大/最小分支)写成 —— 与被测实现的表述方式不同,
    /// 所以生产代码里深度计分的符号错误、备忘表键碰撞之类的 bug 不会在这里同时犯。
    /// 抄一份同样的实现来对比,等于用同一个错误验证它自己。
    /// </para>
    /// </summary>
    private static int Value(Board board, Stone toMove, Dictionary<(int, Stone), int> memo)
    {
        var key = (Encode(board), toMove);
        if (memo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var best = -2;
        foreach (var p in AllEmpties(board))
        {
            var next = board.Clone();
            var result = next.PlaceStone(new DomainMove(p, toMove));
            var value = result switch
            {
                GameResult.Ongoing => -Value(next, Other(toMove), memo),
                GameResult.Draw => 0,
                _ => 1, // 刚落子的一方赢了,而那就是 toMove
            };
            best = Math.Max(best, value);
        }

        memo[key] = best;
        return best;
    }

    /// <summary>终局结果在 <paramref name="stone"/> 视角下的 +1 / 0 / -1。</summary>
    private static int Signed(GameResult result, Stone stone)
    {
        if (result == GameResult.Draw)
        {
            return 0;
        }
        return IsLossFor(result, stone) ? -1 : 1;
    }

    private static void Enumerate(
        Board board, Stone toMove, HashSet<int> visited, List<(Board, Stone)> acc)
    {
        var key = (Encode(board) * 3) + (int)toMove;
        if (!visited.Add(key))
        {
            return;
        }

        acc.Add((board, toMove));

        foreach (var p in AllEmpties(board))
        {
            var next = board.Clone();
            if (next.PlaceStone(new DomainMove(p, toMove)) != GameResult.Ongoing)
            {
                continue; // 终局不再展开
            }
            Enumerate(next, Other(toMove), visited, acc);
        }
    }

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

    private static string Describe(Board board)
        => string.Concat(AllCells(board).Select(s => s switch
        {
            Stone.Black => 'X',
            Stone.White => 'O',
            _ => '.',
        }));

    private static IEnumerable<Stone> AllCells(Board board)
    {
        for (var r = 0; r < board.Rows; r++)
        {
            for (var c = 0; c < board.Cols; c++)
            {
                yield return board.GetStone(new Position(r, c));
            }
        }
    }

    // ---- 完美对弈必和 ----

    [Fact]
    public void Hard_versus_Hard_is_a_draw()
    {
        var board = Rules.CreateBoard();
        var black = new TicTacToeHardAi();
        var white = new TicTacToeHardAi();
        var toMove = Stone.Black;
        GameResult result;

        do
        {
            var ai = toMove == Stone.Black ? black : white;
            result = board.PlaceStone(new DomainMove(ai.SelectMove(board, toMove), toMove));
            toMove = Other(toMove);
        }
        while (result == GameResult.Ongoing);

        result.Should().Be(GameResult.Draw);
    }

    // ---- 有胜必取 ----

    [Fact]
    public void Hard_takes_an_immediate_win_rather_than_merely_not_losing()
    {
        // X X .
        // O O .
        // . . .
        // 轮到 X:(0,2) 立即取胜。堵 (1,2) 也"不输",但那是把胜势换成和棋。
        var board = Rules.CreateBoard();
        board.PlaceStone(new DomainMove(new Position(0, 0), Stone.Black));
        board.PlaceStone(new DomainMove(new Position(1, 0), Stone.White));
        board.PlaceStone(new DomainMove(new Position(0, 1), Stone.Black));
        board.PlaceStone(new DomainMove(new Position(1, 1), Stone.White));

        var pick = new TicTacToeHardAi().SelectMove(board, Stone.Black);

        pick.Should().Be(new Position(0, 2));
    }

    [Fact]
    public void Hard_blocks_when_it_has_no_win_of_its_own()
    {
        // X X .
        // O . .
        // . . .
        // 轮到 O:X 下一手 (0,2) 就赢,必须堵。
        var board = Rules.CreateBoard();
        board.PlaceStone(new DomainMove(new Position(0, 0), Stone.Black));
        board.PlaceStone(new DomainMove(new Position(1, 0), Stone.White));
        board.PlaceStone(new DomainMove(new Position(0, 1), Stone.Black));

        var pick = new TicTacToeHardAi().SelectMove(board, Stone.White);

        pick.Should().Be(new Position(0, 2));
    }

    // ---- 契约 ----

    [Fact]
    public void Hard_does_not_mutate_the_board_it_is_given()
    {
        var board = Rules.CreateBoard();
        board.PlaceStone(new DomainMove(new Position(1, 1), Stone.Black));
        var before = Describe(board);

        new TicTacToeHardAi().SelectMove(board, Stone.White);

        Describe(board).Should().Be(before);
    }

    [Fact]
    public void Hard_is_deterministic()
    {
        var board = Rules.CreateBoard();

        var first = new TicTacToeHardAi().SelectMove(board, Stone.Black);
        var second = new TicTacToeHardAi().SelectMove(board, Stone.Black);

        second.Should().Be(first);
    }

    [Fact]
    public void Hard_rejects_an_empty_stone()
    {
        var act = () => new TicTacToeHardAi().SelectMove(Rules.CreateBoard(), Stone.Empty);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Hard_throws_on_a_full_board()
    {
        // 满盘 9 格但无三连 —— 判胜给的是 Draw,调用方本该到此为止。
        var board = Rules.CreateBoard();
        var layout = new (int R, int C, Stone S)[]
        {
            (0, 0, Stone.Black), (0, 1, Stone.White), (0, 2, Stone.Black),
            (1, 0, Stone.Black), (1, 1, Stone.White), (1, 2, Stone.White),
            (2, 0, Stone.White), (2, 1, Stone.Black), (2, 2, Stone.Black),
        };
        foreach (var (r, c, s) in layout)
        {
            board.PlaceStone(new DomainMove(new Position(r, c), s));
        }

        var act = () => new TicTacToeHardAi().SelectMove(board, Stone.Black);

        act.Should().Throw<InvalidOperationException>();
    }
}
