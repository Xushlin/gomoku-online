using Move = Gomoku.Domain.ValueObjects.Move;

namespace Gomoku.Domain.Tests.Ai;

public class HardAiTests
{
    [Fact]
    public void Empty_Board_First_Move_Is_Center()
    {
        var ai = new HardAi(new Random(42));
        var board = new Board();

        var pick = ai.SelectMove(board, Stone.Black);

        pick.Should().Be(new Position(7, 7));
    }

    [Fact]
    public void Picks_Immediate_Win_When_Own_Four_In_A_Row()
    {
        var board = new Board();
        board.PlaceStone(new Move(new Position(7, 3), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 4), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 5), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 6), Stone.Black));

        var ai = new HardAi(new Random(1));
        var pick = ai.SelectMove(board, Stone.Black);

        pick.Should().BeOneOf(new Position(7, 2), new Position(7, 7));
    }

    [Fact]
    public void Blocks_Opponents_Closed_Four_At_Open_End()
    {
        // White 冲四:(7,3)..(7,6) 四连,左端 (7,2) 被 Black 已封,右端 (7,7) 开 —— White 下一步
        // 必走 (7,7) 赢。用冲四(非活四)保证 Black 有**唯一**正解 (7,7) 堵;
        // 活四两端都能赢,Black 怎么下都输,不能验证 AI 的决策能力。
        var board = new Board();
        board.PlaceStone(new Move(new Position(7, 2), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 3), Stone.White));
        board.PlaceStone(new Move(new Position(7, 4), Stone.White));
        board.PlaceStone(new Move(new Position(7, 5), Stone.White));
        board.PlaceStone(new Move(new Position(7, 6), Stone.White));

        var ai = new HardAi(new Random(1));
        var pick = ai.SelectMove(board, Stone.Black);

        pick.Should().Be(new Position(7, 7));
    }

    [Fact]
    public void Blocks_Opponents_Open_Three()
    {
        // White 活三 (7,5)(7,6)(7,7),两端 (7,4)(7,8) 空;Black 必堵一端
        var board = new Board();
        board.PlaceStone(new Move(new Position(7, 5), Stone.White));
        board.PlaceStone(new Move(new Position(7, 6), Stone.White));
        board.PlaceStone(new Move(new Position(7, 7), Stone.White));
        // Black 远处单子,不构成威胁但让候选不为空集
        board.PlaceStone(new Move(new Position(0, 0), Stone.Black));

        var ai = new HardAi(new Random(1));
        var pick = ai.SelectMove(board, Stone.Black);

        pick.Should().BeOneOf(new Position(7, 4), new Position(7, 8));
    }

    [Fact]
    public void Never_Selects_Occupied_Cell_Across_Many_Samples()
    {
        // 预放若干子,反复 SelectMove 应从不落在已有子上
        var board = new Board();
        var occupied = new (int, int, Stone)[]
        {
            (7, 7, Stone.Black),
            (7, 8, Stone.White),
            (8, 7, Stone.White),
            (6, 6, Stone.Black),
            (6, 7, Stone.Black),
        };
        foreach (var (r, c, s) in occupied)
        {
            board.PlaceStone(new Move(new Position(r, c), s));
        }

        var ai = new HardAi(new Random(1));
        for (var i = 0; i < 50; i++)
        {
            var pick = ai.SelectMove(board, Stone.Black);
            board.GetStone(pick).Should().Be(Stone.Empty);
        }
    }

    [Fact]
    public void Does_Not_Mutate_Input_Board()
    {
        var board = new Board();
        board.PlaceStone(new Move(new Position(7, 3), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 4), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 5), Stone.Black));

        var snapshot = new Stone[Position.BoardSize * Position.BoardSize];
        for (var r = 0; r <= Position.MaxIndex; r++)
            for (var c = 0; c <= Position.MaxIndex; c++)
                snapshot[r * Position.BoardSize + c] = board.GetStone(new Position(r, c));

        var ai = new HardAi(new Random(1));
        _ = ai.SelectMove(board, Stone.Black);

        for (var r = 0; r <= Position.MaxIndex; r++)
            for (var c = 0; c <= Position.MaxIndex; c++)
                board.GetStone(new Position(r, c))
                    .Should().Be(snapshot[r * Position.BoardSize + c], $"at ({r},{c})");
    }

    [Fact]
    public void Fixed_Seed_Reproducible()
    {
        // 构造一个有选择空间的盘面(两个等价最优可能性,让 random 破平参与)
        var boardA = new Board();
        var boardB = new Board();
        boardA.PlaceStone(new Move(new Position(7, 7), Stone.Black));
        boardB.PlaceStone(new Move(new Position(7, 7), Stone.Black));

        var aiA = new HardAi(new Random(42));
        var aiB = new HardAi(new Random(42));

        aiA.SelectMove(boardA, Stone.White).Should().Be(aiB.SelectMove(boardB, Stone.White));
    }

    [Fact]
    public void Empty_Stone_Throws()
    {
        var ai = new HardAi(new Random(1));
        var board = new Board();

        var act = () => ai.SelectMove(board, Stone.Empty);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Null_Random_Throws()
    {
        var act = () => new HardAi(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Zero_SearchDepth_Throws()
    {
        var act = () => new HardAi(new Random(1), searchDepth: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Candidate_Generation_Limits_To_2Grid_Neighbourhood()
    {
        // 唯一一颗子 (7,7),候选应是 5x5 方块减自己 = 24 格;决不含远处 (0,0)
        var board = new Board();
        board.PlaceStone(new Move(new Position(7, 7), Stone.Black));

        var candidates = HardAi.GenerateCandidates(board);

        candidates.Should().HaveCount(24);
        candidates.Should().NotContain(new Position(0, 0));
        candidates.Should().NotContain(new Position(14, 14));
        candidates.Should().NotContain(new Position(7, 7)); // 已占格不在候选
    }

    [Fact]
    public void Evaluate_Rewards_Own_Open_Three()
    {
        // 盘面只有黑方 (7,5)(7,6)(7,7) 活三
        var board = new Board();
        board.PlaceStone(new Move(new Position(7, 5), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 6), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 7), Stone.Black));

        var scoreFromBlack = HardAi.Evaluate(board, Stone.Black);
        var scoreFromWhite = HardAi.Evaluate(board, Stone.White);

        scoreFromBlack.Should().BeGreaterThan(0);
        scoreFromWhite.Should().BeLessThan(0);
    }
}
