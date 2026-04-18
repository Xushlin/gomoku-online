namespace Gomoku.Domain.Tests.Entities;

public class BoardBasicsTests
{
    [Fact]
    public void New_Board_Is_Empty_Everywhere()
    {
        var board = new Board();

        for (var r = 0; r < Position.BoardSize; r++)
        {
            for (var c = 0; c < Position.BoardSize; c++)
            {
                board.GetStone(new Position(r, c)).Should().Be(Stone.Empty);
            }
        }
    }

    [Fact]
    public void PlaceStone_Stores_The_Stone()
    {
        var board = new Board();
        var pos = new Position(7, 7);

        var result = board.PlaceStone(new Move(pos, Stone.Black));

        result.Should().Be(GameResult.Ongoing);
        board.GetStone(pos).Should().Be(Stone.Black);
    }

    [Fact]
    public void PlaceStone_On_Occupied_Cell_Throws_And_Leaves_Board_Unchanged()
    {
        var board = new Board();
        var pos = new Position(7, 7);
        board.PlaceStone(new Move(pos, Stone.Black));

        var act = () => board.PlaceStone(new Move(pos, Stone.White));

        act.Should()
            .Throw<InvalidMoveException>()
            .WithMessage("*(7, 7)*");
        board.GetStone(pos).Should().Be(Stone.Black);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(15, 0)]
    [InlineData(0, 15)]
    public void Constructing_Position_With_Out_Of_Range_Coords_Throws(int row, int col)
    {
        var act = () => new Position(row, col);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Clone_Mutation_Does_Not_Affect_Original()
    {
        var original = new Board();
        original.PlaceStone(new Move(new Position(7, 7), Stone.Black));
        var clone = original.Clone();

        clone.PlaceStone(new Move(new Position(7, 8), Stone.White));

        original.GetStone(new Position(7, 8)).Should().Be(Stone.Empty);
        clone.GetStone(new Position(7, 8)).Should().Be(Stone.White);
    }

    [Fact]
    public void Original_Mutation_Does_Not_Affect_Clone()
    {
        var original = new Board();
        original.PlaceStone(new Move(new Position(7, 7), Stone.Black));
        var clone = original.Clone();

        original.PlaceStone(new Move(new Position(7, 8), Stone.White));

        clone.GetStone(new Position(7, 8)).Should().Be(Stone.Empty);
        original.GetStone(new Position(7, 8)).Should().Be(Stone.White);
    }

    [Fact]
    public void Reset_Clears_All_Cells()
    {
        var board = new Board();
        board.PlaceStone(new Move(new Position(0, 0), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 7), Stone.White));
        board.PlaceStone(new Move(new Position(14, 14), Stone.Black));

        board.Reset();

        for (var r = 0; r < Position.BoardSize; r++)
        {
            for (var c = 0; c < Position.BoardSize; c++)
            {
                board.GetStone(new Position(r, c)).Should().Be(Stone.Empty);
            }
        }
    }

    [Fact]
    public void Reset_Allows_Subsequent_Placement()
    {
        var board = new Board();
        board.PlaceStone(new Move(new Position(7, 7), Stone.Black));
        board.Reset();

        var result = board.PlaceStone(new Move(new Position(7, 7), Stone.Black));

        result.Should().Be(GameResult.Ongoing);
        board.GetStone(new Position(7, 7)).Should().Be(Stone.Black);
    }
}
