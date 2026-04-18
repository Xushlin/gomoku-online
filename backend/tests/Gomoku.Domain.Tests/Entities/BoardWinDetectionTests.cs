namespace Gomoku.Domain.Tests.Entities;

public class BoardWinDetectionTests
{
    private static void PlaceAll(Board board, IEnumerable<Move> moves)
    {
        foreach (var move in moves)
        {
            board.PlaceStone(move);
        }
    }

    [Fact]
    public void Horizontal_Five_In_A_Row_Yields_BlackWin()
    {
        var board = new Board();
        PlaceAll(board, new[]
        {
            new Move(new Position(7, 3), Stone.Black),
            new Move(new Position(7, 4), Stone.Black),
            new Move(new Position(7, 5), Stone.Black),
            new Move(new Position(7, 6), Stone.Black),
        });

        var result = board.PlaceStone(new Move(new Position(7, 7), Stone.Black));

        result.Should().Be(GameResult.BlackWin);
    }

    [Fact]
    public void Horizontal_Five_In_A_Row_Yields_WhiteWin()
    {
        var board = new Board();
        PlaceAll(board, new[]
        {
            new Move(new Position(7, 3), Stone.White),
            new Move(new Position(7, 4), Stone.White),
            new Move(new Position(7, 5), Stone.White),
            new Move(new Position(7, 6), Stone.White),
        });

        var result = board.PlaceStone(new Move(new Position(7, 7), Stone.White));

        result.Should().Be(GameResult.WhiteWin);
    }

    [Fact]
    public void Vertical_Five_In_A_Row_Yields_BlackWin()
    {
        var board = new Board();
        PlaceAll(board, new[]
        {
            new Move(new Position(3, 7), Stone.Black),
            new Move(new Position(4, 7), Stone.Black),
            new Move(new Position(5, 7), Stone.Black),
            new Move(new Position(6, 7), Stone.Black),
        });

        var result = board.PlaceStone(new Move(new Position(7, 7), Stone.Black));

        result.Should().Be(GameResult.BlackWin);
    }

    [Fact]
    public void Main_Diagonal_Five_In_A_Row_Yields_BlackWin()
    {
        var board = new Board();
        PlaceAll(board, new[]
        {
            new Move(new Position(3, 3), Stone.Black),
            new Move(new Position(4, 4), Stone.Black),
            new Move(new Position(5, 5), Stone.Black),
            new Move(new Position(6, 6), Stone.Black),
        });

        var result = board.PlaceStone(new Move(new Position(7, 7), Stone.Black));

        result.Should().Be(GameResult.BlackWin);
    }

    [Fact]
    public void Anti_Diagonal_Five_In_A_Row_Yields_WhiteWin()
    {
        var board = new Board();
        PlaceAll(board, new[]
        {
            new Move(new Position(7, 3), Stone.White),
            new Move(new Position(6, 4), Stone.White),
            new Move(new Position(5, 5), Stone.White),
            new Move(new Position(4, 6), Stone.White),
        });

        var result = board.PlaceStone(new Move(new Position(3, 7), Stone.White));

        result.Should().Be(GameResult.WhiteWin);
    }

    [Fact]
    public void Six_In_A_Row_Long_Connect_Still_Wins()
    {
        var board = new Board();
        PlaceAll(board, new[]
        {
            new Move(new Position(7, 2), Stone.Black),
            new Move(new Position(7, 3), Stone.Black),
            new Move(new Position(7, 4), Stone.Black),
            new Move(new Position(7, 5), Stone.Black),
            new Move(new Position(7, 6), Stone.Black),
        });

        var result = board.PlaceStone(new Move(new Position(7, 7), Stone.Black));

        result.Should().Be(GameResult.BlackWin);
    }

    [Fact]
    public void Five_On_Top_Edge_Yields_BlackWin()
    {
        var board = new Board();
        PlaceAll(board, new[]
        {
            new Move(new Position(0, 0), Stone.Black),
            new Move(new Position(0, 1), Stone.Black),
            new Move(new Position(0, 2), Stone.Black),
            new Move(new Position(0, 3), Stone.Black),
        });

        var result = board.PlaceStone(new Move(new Position(0, 4), Stone.Black));

        result.Should().Be(GameResult.BlackWin);
    }

    [Fact]
    public void Five_On_Bottom_Edge_Yields_WhiteWin()
    {
        var board = new Board();
        PlaceAll(board, new[]
        {
            new Move(new Position(14, 10), Stone.White),
            new Move(new Position(14, 11), Stone.White),
            new Move(new Position(14, 12), Stone.White),
            new Move(new Position(14, 13), Stone.White),
        });

        var result = board.PlaceStone(new Move(new Position(14, 14), Stone.White));

        result.Should().Be(GameResult.WhiteWin);
    }

    [Fact]
    public void Five_On_Left_Edge_Yields_BlackWin()
    {
        var board = new Board();
        PlaceAll(board, new[]
        {
            new Move(new Position(10, 0), Stone.Black),
            new Move(new Position(11, 0), Stone.Black),
            new Move(new Position(12, 0), Stone.Black),
            new Move(new Position(13, 0), Stone.Black),
        });

        var result = board.PlaceStone(new Move(new Position(14, 0), Stone.Black));

        result.Should().Be(GameResult.BlackWin);
    }

    [Fact]
    public void Five_On_Right_Edge_Yields_WhiteWin()
    {
        var board = new Board();
        PlaceAll(board, new[]
        {
            new Move(new Position(0, 14), Stone.White),
            new Move(new Position(1, 14), Stone.White),
            new Move(new Position(2, 14), Stone.White),
            new Move(new Position(3, 14), Stone.White),
        });

        var result = board.PlaceStone(new Move(new Position(4, 14), Stone.White));

        result.Should().Be(GameResult.WhiteWin);
    }

    [Fact]
    public void Four_In_A_Row_Without_Fifth_Stays_Ongoing()
    {
        var board = new Board();

        var last = board.PlaceStone(new Move(new Position(7, 3), Stone.Black));
        last.Should().Be(GameResult.Ongoing);
        last = board.PlaceStone(new Move(new Position(7, 4), Stone.Black));
        last.Should().Be(GameResult.Ongoing);
        last = board.PlaceStone(new Move(new Position(7, 5), Stone.Black));
        last.Should().Be(GameResult.Ongoing);
        last = board.PlaceStone(new Move(new Position(7, 6), Stone.Black));
        last.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void Four_Plus_Four_Broken_By_Opponent_Is_Not_A_Win()
    {
        var board = new Board();
        PlaceAll(board, new[]
        {
            new Move(new Position(7, 3), Stone.Black),
            new Move(new Position(7, 4), Stone.Black),
            new Move(new Position(7, 5), Stone.White),
            new Move(new Position(7, 6), Stone.Black),
        });

        var result = board.PlaceStone(new Move(new Position(7, 7), Stone.Black));

        result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void Full_Board_Without_Five_Yields_Draw()
    {
        var board = new Board();

        // Pattern: color(r,c) = ((r + 2*c) % 4) < 2 ? Black : White
        // 保证水平、竖直、两对角方向的最长同色连子数均 ≤ 2 < 5。
        // 验证过程中没有任何一步会形成五连,所以每一步中途也不会提前判胜。
        GameResult last = GameResult.Ongoing;
        for (var r = 0; r < Position.BoardSize; r++)
        {
            for (var c = 0; c < Position.BoardSize; c++)
            {
                var color = ((r + 2 * c) % 4) < 2 ? Stone.Black : Stone.White;
                last = board.PlaceStone(new Move(new Position(r, c), color));
            }
        }

        last.Should().Be(GameResult.Draw);
    }

    [Fact]
    public void Ongoing_Before_Last_Cell_Is_Filled()
    {
        var board = new Board();

        GameResult last = GameResult.Ongoing;
        for (var r = 0; r < Position.BoardSize; r++)
        {
            for (var c = 0; c < Position.BoardSize; c++)
            {
                if (r == Position.MaxIndex && c == Position.MaxIndex)
                {
                    break;
                }

                var color = ((r + 2 * c) % 4) < 2 ? Stone.Black : Stone.White;
                last = board.PlaceStone(new Move(new Position(r, c), color));
                last.Should().Be(GameResult.Ongoing);
            }
        }
    }
}
