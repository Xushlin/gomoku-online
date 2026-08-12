using Move = Gomoku.Domain.ValueObjects.Move;

namespace Gomoku.Domain.Tests.Ai;

public class EasyAiTests
{
    [Fact]
    public void SelectMove_On_Empty_Board_Returns_Legal_Position()
    {
        var ai = new EasyAi(new Random(42));
        var board = new Board();

        var pick = ai.SelectMove(board, Stone.Black);

        board.GetStone(pick).Should().Be(Stone.Empty);
        pick.Row.Should().BeInRange(0, Position.MaxIndex);
        pick.Col.Should().BeInRange(0, Position.MaxIndex);
    }

    [Fact]
    public void SelectMove_With_Fixed_Seed_Is_Reproducible()
    {
        var aiA = new EasyAi(new Random(42));
        var aiB = new EasyAi(new Random(42));
        var boardA = new Board();
        var boardB = new Board();

        var pickA = aiA.SelectMove(boardA, Stone.Black);
        var pickB = aiB.SelectMove(boardB, Stone.Black);

        pickA.Should().Be(pickB);
    }

    [Fact]
    public void SelectMove_Never_Returns_Occupied_Position()
    {
        // 预置 10 颗子 + 连续 1000 次采样(每次推进 Random),全部落点必为空格。
        var occupied = new (int, int)[]
        {
            (0, 0), (0, 1), (3, 5), (7, 7), (8, 8),
            (10, 2), (10, 10), (14, 14), (2, 13), (6, 4),
        };
        var board = new Board();
        var turn = Stone.Black;
        foreach (var (r, c) in occupied)
        {
            board.PlaceStone(new Move(new Position(r, c), turn));
            turn = turn == Stone.Black ? Stone.White : Stone.Black;
        }

        var ai = new EasyAi(new Random(123));
        for (var i = 0; i < 1000; i++)
        {
            var pick = ai.SelectMove(board, Stone.Black);
            board.GetStone(pick).Should().Be(Stone.Empty);
            occupied.Should().NotContain((pick.Row, pick.Col));
        }
    }

    [Fact]
    public void SelectMove_On_Board_With_Only_One_Empty_Returns_That_Cell()
    {
        // 填满除 (7,7) 之外的所有格,注意要**避免**连五否则触发 GameResult 而
        // 不是占满;本测试不需要 Board.Place 路径合法性,所以直接用反射?不可能。
        // 改用 Clone + 手工循环 —— 实际上 Board 没暴露"直接设置"的 API,所以
        // 用逐步 PlaceStone 并确保不连 5。策略:纵横交错,对每行用同色,行间交替
        // 黑白,这样一行 15 颗同色但行间无纵向同色串,确保 PlaceStone 不返回 Win。
        // 等等 —— 一行全同色 15 颗就连 5+ 了,必然触发 Win。所以我们改成:
        // 每 4 颗同色后换色,同行内 4 黑 4 白 4 黑 3 白 = 不触发。
        var board = new Board();
        var reserved = new Position(7, 7);
        for (var r = 0; r <= Position.MaxIndex; r++)
        {
            for (var c = 0; c <= Position.MaxIndex; c++)
            {
                if (r == reserved.Row && c == reserved.Col) continue;
                // 按 (r*15+c) 的 index / 4 奇偶交替,防止 5 连。
                // 用 (r + 2c) % 4 < 2 的 pattern 填色 —— 已验证在 4 个方向上都不会
                // 产生 5 连(水平 BWBW、垂直 BBWW、主对角 BWWBBWW、反对角同理,最长 2 同色)。
                var color = (r + 2 * c) % 4 < 2 ? Stone.Black : Stone.White;
                var res = board.PlaceStone(new Move(new Position(r, c), color));
                // 任何时候触发胜利都说明测试构造失败
                res.Should().Be(GameResult.Ongoing, $"填格到 ({r},{c}) 时不应触发胜利");
            }
        }

        var ai = new EasyAi(new Random(7));
        var pick = ai.SelectMove(board, Stone.Black);

        pick.Should().Be(reserved);
    }

    [Fact]
    public void SelectMove_With_Empty_Stone_Throws()
    {
        var ai = new EasyAi(new Random(1));
        var board = new Board();

        var act = () => ai.SelectMove(board, Stone.Empty);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SelectMove_On_Full_Board_Throws()
    {
        // 用同样的 "每 4 颗换色" 套路填满整盘(前测试已证明该模式不触发胜利,
        // 但只剩 0 空格时 Board.PlaceStone 的最后一子会因"棋盘已满"返回 Draw
        // 而不是抛异常;所以这里我们填到 **不剩空格** 为止)。
        var board = new Board();
        for (var r = 0; r <= Position.MaxIndex; r++)
        {
            for (var c = 0; c <= Position.MaxIndex; c++)
            {
                // 用 (r + 2c) % 4 < 2 的 pattern 填色 —— 已验证在 4 个方向上都不会
                // 产生 5 连(水平 BWBW、垂直 BBWW、主对角 BWWBBWW、反对角同理,最长 2 同色)。
                var color = (r + 2 * c) % 4 < 2 ? Stone.Black : Stone.White;
                board.PlaceStone(new Move(new Position(r, c), color));
            }
        }

        var ai = new EasyAi(new Random(1));
        var act = () => ai.SelectMove(board, Stone.Black);

        act.Should().Throw<InvalidOperationException>();
    }
}
