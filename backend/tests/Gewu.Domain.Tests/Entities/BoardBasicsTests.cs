using Move = Gewu.Domain.ValueObjects.Move;

namespace Gewu.Domain.Tests.Entities;

public class BoardBasicsTests
{
    [Fact]
    public void New_Board_Is_Empty_Everywhere()
    {
        var board = GomokuBoards.New();

        for (var r = 0; r < GomokuBoards.Size; r++)
        {
            for (var c = 0; c < GomokuBoards.Size; c++)
            {
                board.GetStone(new Position(r, c)).Should().Be(Stone.Empty);
            }
        }
    }

    [Fact]
    public void PlaceStone_Stores_The_Stone()
    {
        var board = GomokuBoards.New();
        var pos = new Position(7, 7);

        var result = board.PlaceStone(new Move(pos, Stone.Black));

        result.Should().Be(GameResult.Ongoing);
        board.GetStone(pos).Should().Be(Stone.Black);
    }

    [Fact]
    public void PlaceStone_On_Occupied_Cell_Throws_And_Leaves_Board_Unchanged()
    {
        var board = GomokuBoards.New();
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
    public void Constructing_Position_With_Negative_Coords_Throws(int row, int col)
    {
        var act = () => new Position(row, col);

        act.Should().Throw<InvalidMoveException>();
    }

    [Theory]
    [InlineData(15, 0)]
    [InlineData(0, 15)]
    public void Board_Rejects_Coords_Beyond_Its_Own_Size(int row, int col)
    {
        // 上界从 `Position` 搬到了棋盘 / 规则:坐标本身合法(非负),但这块 15×15 的盘
        // 装不下它。抛的仍是 InvalidMoveException,对外的 409 因此不动。
        var board = GomokuBoards.New();

        var act = () => board.GetStone(new Position(row, col));

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Clone_Mutation_Does_Not_Affect_Original()
    {
        var original = GomokuBoards.New();
        original.PlaceStone(new Move(new Position(7, 7), Stone.Black));
        var clone = original.Clone();

        clone.PlaceStone(new Move(new Position(7, 8), Stone.White));

        original.GetStone(new Position(7, 8)).Should().Be(Stone.Empty);
        clone.GetStone(new Position(7, 8)).Should().Be(Stone.White);
    }

    [Fact]
    public void Original_Mutation_Does_Not_Affect_Clone()
    {
        var original = GomokuBoards.New();
        original.PlaceStone(new Move(new Position(7, 7), Stone.Black));
        var clone = original.Clone();

        original.PlaceStone(new Move(new Position(7, 8), Stone.White));

        clone.GetStone(new Position(7, 8)).Should().Be(Stone.Empty);
        original.GetStone(new Position(7, 8)).Should().Be(Stone.White);
    }

    [Fact]
    public void Reset_Clears_All_Cells()
    {
        var board = GomokuBoards.New();
        board.PlaceStone(new Move(new Position(0, 0), Stone.Black));
        board.PlaceStone(new Move(new Position(7, 7), Stone.White));
        board.PlaceStone(new Move(new Position(14, 14), Stone.Black));

        board.Reset();

        for (var r = 0; r < GomokuBoards.Size; r++)
        {
            for (var c = 0; c < GomokuBoards.Size; c++)
            {
                board.GetStone(new Position(r, c)).Should().Be(Stone.Empty);
            }
        }
    }

    [Fact]
    public void Reset_Allows_Subsequent_Placement()
    {
        var board = GomokuBoards.New();
        board.PlaceStone(new Move(new Position(7, 7), Stone.Black));
        board.Reset();

        var result = board.PlaceStone(new Move(new Position(7, 7), Stone.Black));

        result.Should().Be(GameResult.Ongoing);
        board.GetStone(new Position(7, 7)).Should().Be(Stone.Black);
    }
}
