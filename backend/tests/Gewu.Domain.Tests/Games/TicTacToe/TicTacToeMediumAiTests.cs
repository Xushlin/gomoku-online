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
/// 一字棋 Medium 档:自赢 → 堵对手 → 中心 → 角 → 随机。
/// <para>
/// 这一档的验收标准不是"下得好",而是**优先级顺序正确**且**可以被击败**。三档之间的区别
/// 必须是玩家能观察到的,否则难度选择器只是装饰。
/// </para>
/// </summary>
public class TicTacToeMediumAiTests
{
    private static readonly INInARowRules Rules = BuiltInGameRules.TicTacToe;

    private static Board Board(params (int R, int C, Stone S)[] stones)
    {
        var board = Rules.CreateBoard();
        foreach (var (r, c, s) in stones)
        {
            board.PlaceStone(new DomainMove(new Position(r, c), s));
        }
        return board;
    }

    private static TicTacToeMediumAi Ai(int seed = 1) => new(new Random(seed));

    // ---- ① 自赢优先 ----

    [Fact]
    public void Takes_the_win_when_a_win_and_a_block_are_both_available()
    {
        // X X .
        // O O .
        // . . .
        // 轮到 X:(0,2) 自己赢,(1,2) 堵对手。两者都存在时,必须选赢 ——
        // 顺序反过来就是把胜势让掉,那是这一档最容易写错的一处。
        var board = Board(
            (0, 0, Stone.Black), (0, 1, Stone.Black),
            (1, 0, Stone.White), (1, 1, Stone.White));

        Ai().SelectMove(board, Stone.Black).Should().Be(new Position(0, 2));
    }

    [Theory]
    [InlineData(0, 0, 1, 1, 2, 2)] // ↘ 对角
    [InlineData(0, 2, 1, 1, 2, 0)] // ↗ 对角
    [InlineData(0, 0, 0, 1, 0, 2)] // 横
    [InlineData(0, 0, 1, 0, 2, 0)] // 竖
    public void Finds_its_own_win_in_every_direction(
        int r1, int c1, int r2, int c2, int r3, int c3)
    {
        // 判胜借道 Board.PlaceStone,所以四个方向都该被覆盖 —— 若哪天有人在 AI 里
        // 手写了一份判胜,这组 Theory 是最先挂的地方。
        var board = Board(
            (r1, c1, Stone.Black), (r2, c2, Stone.Black),
            // 给白方两颗互不成线的子,让局面合法且轮次说得通。
            (1, 2, Stone.White), (2, 1, Stone.White));

        // 上面两颗白子可能正好落在待测的取胜点上,那样这个用例本身不成立 —— 跳过。
        var target = new Position(r3, c3);
        if (board.GetStone(target) != Stone.Empty)
        {
            return;
        }

        Ai().SelectMove(board, Stone.Black).Should().Be(target);
    }

    // ---- ② 无胜可取时堵 ----

    [Fact]
    public void Blocks_when_it_has_no_win_of_its_own()
    {
        // X X .
        // O . .
        // . . .
        // 轮到 O:自己没有立即取胜点,X 下一手 (0,2) 就赢 —— 必须堵。
        var board = Board(
            (0, 0, Stone.Black), (0, 1, Stone.Black),
            (1, 0, Stone.White));

        Ai().SelectMove(board, Stone.White).Should().Be(new Position(0, 2));
    }

    [Fact]
    public void Blocks_a_diagonal_threat()
    {
        // X . .
        // . X .
        // . . .      轮到 O:X 的 ↘ 对角差 (2,2)。中心已被占,所以不会误走 ③。
        var board = Board((0, 0, Stone.Black), (1, 1, Stone.Black));

        Ai().SelectMove(board, Stone.White).Should().Be(new Position(2, 2));
    }

    // ---- ③ 中心 ----

    [Fact]
    public void Opens_at_the_centre()
    {
        Ai().SelectMove(Rules.CreateBoard(), Stone.Black).Should().Be(new Position(1, 1));
    }

    [Fact]
    public void Takes_the_centre_when_it_is_free_and_nothing_is_urgent()
    {
        // X . .
        // . . .
        // . . .      轮到 O:没有取胜点、没有要堵的三连威胁,中心空着。
        var board = Board((0, 0, Stone.Black));

        Ai().SelectMove(board, Stone.White).Should().Be(new Position(1, 1));
    }

    // ---- ④ 角 ----

    [Fact]
    public void Prefers_a_corner_over_an_edge_when_the_centre_is_taken()
    {
        // . . .
        // . X .
        // . . .      轮到 O:中心被占,该走角。角参与三条线,边只参与两条。
        var board = Board((1, 1, Stone.Black));

        var pick = Ai().SelectMove(board, Stone.White);

        TicTacToeCorners.Should().Contain(pick);
    }

    [Fact]
    public void Corner_choice_varies_with_the_random_source()
    {
        // 并列打破必须走注入的 Random —— 否则"随机"是假的,而假的随机会让这一档
        // 每局开出一模一样的棋。
        var board = Board((1, 1, Stone.Black));

        var picks = Enumerable.Range(1, 40)
            .Select(seed => Ai(seed).SelectMove(board, Stone.White))
            .Distinct()
            .ToList();

        picks.Should().HaveCountGreaterThan(1);
        picks.Should().OnlyContain(p => TicTacToeCorners.Contains(p));
    }

    private static readonly Position[] TicTacToeCorners =
    [
        new(0, 0), new(0, 2), new(2, 0), new(2, 2),
    ];

    // ---- ⑤ 兜底与契约 ----

    [Fact]
    public void Falls_back_to_an_edge_when_centre_and_corners_are_gone()
    {
        // X . X
        // . O .
        // X . X      四角与中心都占了,只剩四条边。轮到 O。
        var board = Board(
            (0, 0, Stone.Black), (0, 2, Stone.Black),
            (1, 1, Stone.White),
            (2, 0, Stone.Black), (2, 2, Stone.Black));

        var pick = Ai().SelectMove(board, Stone.White);

        board.GetStone(pick).Should().Be(Stone.Empty);
        TicTacToeCorners.Should().NotContain(pick);
    }

    [Fact]
    public void Does_not_mutate_the_board_it_is_given()
    {
        // 它靠试走判断"这一手能不能赢",所以必须在 Clone 上试 —— 直接在入参上落子
        // 会让调用方拿到一块被污染的盘,而那种 bug 只在生产的 replay 路径上才现形。
        var board = Board((0, 0, Stone.Black), (0, 1, Stone.Black), (1, 0, Stone.White));
        var before = Snapshot(board);

        Ai().SelectMove(board, Stone.White);

        Snapshot(board).Should().Be(before);
    }

    private static string Snapshot(Board board)
    {
        var chars = new char[board.CellCount];
        var i = 0;
        for (var r = 0; r < board.Rows; r++)
        {
            for (var c = 0; c < board.Cols; c++)
            {
                chars[i++] = board.GetStone(new Position(r, c)) switch
                {
                    Stone.Black => 'X',
                    Stone.White => 'O',
                    _ => '.',
                };
            }
        }
        return new string(chars);
    }

    [Fact]
    public void Rejects_an_empty_stone()
    {
        var act = () => Ai().SelectMove(Rules.CreateBoard(), Stone.Empty);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rejects_a_null_random_source()
    {
        var act = () => new TicTacToeMediumAi(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Throws_on_a_full_board()
    {
        var board = Board(
            (0, 0, Stone.Black), (0, 1, Stone.White), (0, 2, Stone.Black),
            (1, 0, Stone.Black), (1, 1, Stone.White), (1, 2, Stone.White),
            (2, 0, Stone.White), (2, 1, Stone.Black), (2, 2, Stone.Black));

        var act = () => Ai().SelectMove(board, Stone.Black);

        act.Should().Throw<InvalidOperationException>();
    }

    // ---- 难度阶梯 ----

    [Fact]
    public void Medium_is_beatable_by_Hard()
    {
        // 这一档存在的意义就是"比 Easy 强、比 Hard 弱"。Hard 执后手也至少不输;
        // 若 Medium 强到能和 Hard 打成平手且从不失误,它和 Hard 就没区别了。
        // 断言只要求 Hard 不输 —— Medium 具体在第几手露出双威胁不该被钉死。
        var board = Rules.CreateBoard();
        var medium = Ai();
        var hard = new TicTacToeHardAi();
        var toMove = Stone.Black; // Medium 先手
        GameResult result;
        Stone mover;

        do
        {
            IPlacementAi ai = toMove == Stone.Black ? medium : hard;
            mover = toMove;
            result = board.PlaceStone(new DomainMove(ai.SelectMove(board, toMove), toMove));
            toMove = toMove == Stone.Black ? Stone.White : Stone.Black;
        }
        while (result == GameResult.Ongoing);

        // "Medium 赢了"现在是 `判胜 && 最后落子的是 Medium` —— `Decided` 自己不说谁赢,
        // 而这里最后一手的落子方正是答案。
        (result == GameResult.Decided && mover == Stone.Black).Should().BeFalse(
            "Hard MUST NOT lose to Medium");
    }
}
