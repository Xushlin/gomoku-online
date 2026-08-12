using Move = Gomoku.Domain.ValueObjects.Move;

namespace Gomoku.Domain.Tests.Ai;

public class MediumAiTests
{
    [Fact]
    public void Prefers_Winning_Move_When_Own_Four_In_A_Row_Present()
    {
        // 黑方在 (7,3)..(7,6) 已有 4 连;连五空点是 (7,2) 或 (7,7)。
        var board = new Board();
        board.PlaceStone(new Move(new Position(7, 3), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 4), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 5), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 6), Stone.Black));

        var ai = new MediumAi(new Random(1));
        var pick = ai.SelectMove(board, Stone.Black);

        pick.Should().BeOneOf(new Position(7, 2), new Position(7, 7));
    }

    [Fact]
    public void Blocks_Opponent_Winning_Move_When_No_Own_Win_Available()
    {
        // 白方在 (7,3)..(7,6) 已有 4 连;黑方必须堵 (7,2) 或 (7,7)。
        // 黑方在盘上另处有一子,不足以自赢。
        var board = new Board();
        board.PlaceStone(new Move(new Position(7, 3), Stone.White));
        board.PlaceStone(new Move(new Position(7, 4), Stone.White));
        board.PlaceStone(new Move(new Position(7, 5), Stone.White));
        board.PlaceStone(new Move(new Position(7, 6), Stone.White));
        board.PlaceStone(new Move(new Position(0, 0), Stone.Black));

        var ai = new MediumAi(new Random(1));
        var pick = ai.SelectMove(board, Stone.Black);

        pick.Should().BeOneOf(new Position(7, 2), new Position(7, 7));
    }

    [Fact]
    public void Self_Win_Beats_Block_When_Both_Available()
    {
        // 黑方能连五 (7,3)..(7,7);同时白方也摆了一个即将连五(6,3)..(6,6),
        // 需要堵 (6,2) 或 (6,7)。按策略应选自赢,而非堵白。
        var board = new Board();
        // 黑 (7,3)(7,4)(7,5)(7,6) 等待 (7,7) 连五
        board.PlaceStone(new Move(new Position(7, 3), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 4), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 5), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 6), Stone.Black));
        // 白 (6,3)(6,4)(6,5)(6,6) 威胁 (6,2) / (6,7)
        board.PlaceStone(new Move(new Position(6, 3), Stone.White));
        board.PlaceStone(new Move(new Position(6, 4), Stone.White));
        board.PlaceStone(new Move(new Position(6, 5), Stone.White));
        board.PlaceStone(new Move(new Position(6, 6), Stone.White));

        var ai = new MediumAi(new Random(1));
        var pick = ai.SelectMove(board, Stone.Black);

        // 自赢点:(7,2) 或 (7,7);堵点:(6,2) 或 (6,7)
        pick.Should().BeOneOf(new Position(7, 2), new Position(7, 7));
    }

    [Fact]
    public void Empty_Board_First_Move_Picks_Center()
    {
        // 空盘上,第一层第二层都不命中;启发分选最高。
        // centerPenalty 在 (7,7) 为 0(最优),其他所有点 centerPenalty < 0;
        // adjacency 对空盘恒为 0。故 (7,7) 唯一最高分。
        var board = new Board();
        var ai = new MediumAi(new Random(1));

        var pick = ai.SelectMove(board, Stone.Black);

        pick.Should().Be(new Position(7, 7));
    }

    [Fact]
    public void Does_Not_Mutate_Input_Board()
    {
        // AI 只能通过 board.Clone() 试走;入参 Board 调用前后完全一致。
        var board = new Board();
        board.PlaceStone(new Move(new Position(7, 3), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 4), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 5), Stone.Black));

        // 拍照
        var snapshot = new Stone[Position.BoardSize * Position.BoardSize];
        for (var r = 0; r <= Position.MaxIndex; r++)
            for (var c = 0; c <= Position.MaxIndex; c++)
                snapshot[r * Position.BoardSize + c] = board.GetStone(new Position(r, c));

        var ai = new MediumAi(new Random(1));
        _ = ai.SelectMove(board, Stone.Black);

        for (var r = 0; r <= Position.MaxIndex; r++)
            for (var c = 0; c <= Position.MaxIndex; c++)
                board.GetStone(new Position(r, c))
                    .Should().Be(snapshot[r * Position.BoardSize + c], $"at ({r},{c})");
    }

    [Fact]
    public void Empty_Stone_Throws()
    {
        var ai = new MediumAi(new Random(1));
        var board = new Board();

        var act = () => ai.SelectMove(board, Stone.Empty);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Never_Selects_Occupied_Cell()
    {
        // 放一些子,然后让 MediumAi 走;结果必为空格。
        var board = new Board();
        var placed = new (int, int, Stone)[]
        {
            (7, 7, Stone.Black),
            (7, 8, Stone.White),
            (8, 7, Stone.White),
            (6, 6, Stone.Black),
        };
        foreach (var (r, c, s) in placed)
        {
            board.PlaceStone(new Move(new Position(r, c), s));
        }

        var ai = new MediumAi(new Random(1));
        var pick = ai.SelectMove(board, Stone.Black);

        board.GetStone(pick).Should().Be(Stone.Empty);
    }

    [Fact]
    public void Heuristic_Prefers_Cell_Adjacent_To_Own_Stone_Over_Center_Distance_Tie()
    {
        // 让 (7,7) 被己方占,AI 要找次优。启发分:
        //   (7,6)/(7,8)/(6,7)/(8,7) 等相邻格 centerPenalty=-1、adjacency=1 → 总 0
        //   远处格子 centerPenalty 更低(更负)、adjacency=0 → 更低
        // 故 AI 应选与 (7,7) 相邻的 8 格之一。
        var board = new Board();
        board.PlaceStone(new Move(new Position(7, 7), Stone.Black));

        var ai = new MediumAi(new Random(1));
        var pick = ai.SelectMove(board, Stone.Black);

        // 8 邻域
        var expectedNeighbours = new HashSet<(int, int)>
        {
            (6, 6), (6, 7), (6, 8),
            (7, 6),         (7, 8),
            (8, 6), (8, 7), (8, 8),
        };
        expectedNeighbours.Should().Contain((pick.Row, pick.Col));
    }
}
