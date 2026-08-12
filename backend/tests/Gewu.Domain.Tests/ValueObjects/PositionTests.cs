namespace Gomoku.Domain.Tests.ValueObjects;

public class PositionTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(14, 14)]
    [InlineData(7, 7)]
    [InlineData(0, 14)]
    [InlineData(14, 0)]
    public void Valid_Coordinates_Construct_Successfully(int row, int col)
    {
        var pos = new Position(row, col);

        pos.Row.Should().Be(row);
        pos.Col.Should().Be(col);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(15)]
    [InlineData(100)]
    public void Out_Of_Range_Row_Throws(int row)
    {
        var act = () => new Position(row, 0);

        act.Should()
            .Throw<InvalidMoveException>()
            .WithMessage($"*row {row}*[0..14]*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(15)]
    [InlineData(100)]
    public void Out_Of_Range_Col_Throws(int col)
    {
        var act = () => new Position(0, col);

        act.Should()
            .Throw<InvalidMoveException>()
            .WithMessage($"*col {col}*[0..14]*");
    }

    [Fact]
    public void Equal_Coordinates_Are_Value_Equal()
    {
        var a = new Position(3, 4);
        var b = new Position(3, 4);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Different_Coordinates_Are_Not_Equal()
    {
        var a = new Position(3, 4);
        var b = new Position(4, 3);

        a.Should().NotBe(b);
        (a == b).Should().BeFalse();
    }

    [Fact]
    public void Board_Size_Constant_Is_15()
    {
        Position.BoardSize.Should().Be(15);
        Position.MaxIndex.Should().Be(14);
    }
}
