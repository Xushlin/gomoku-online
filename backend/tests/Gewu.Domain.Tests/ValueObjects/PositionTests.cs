using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.NInARow;
namespace Gewu.Domain.Tests.ValueObjects;

public class PositionTests
{

    /// <summary>
    /// 越界判定现在是 <c>Apply</c> 的内部一步(<c>IsInBounds</c> 不再是公开成员)——
    /// 盘面语义整个属于规则。这个 helper 通过公开面问同一个问题,断言的行为一字未变。
    /// </summary>
    private static bool AcceptsPlacement(IGameRules rules, Position position)
    {
        try
        {
            rules.Apply([], MoveIntent.Place(position), Stone.Black);
            return true;
        }
        catch (InvalidMoveException)
        {
            return false;
        }
    }
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
    [InlineData(-100)]
    public void Negative_Row_Throws(int row)
    {
        var act = () => new Position(row, 0);

        act.Should()
            .Throw<InvalidMoveException>()
            .WithMessage($"*row {row}*negative*");
    }

    [Theory]
    [InlineData(15)]
    [InlineData(100)]
    public void Row_Beyond_A_Gomoku_Board_Is_Accepted_By_Position_Itself(int row)
    {
        // 上界搬到了棋种规则上:`Position` 只保证非负,15 在五子棋上越界、在假想的
        // 21×21 棋种上合法,所以坐标类型本身不该有意见。真正的拒绝在下面那条测试。
        var act = () => new Position(row, 0);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(15)]
    [InlineData(100)]
    public void Row_Beyond_A_Gomoku_Board_Is_Rejected_By_The_Rules(int row)
    {
        AcceptsPlacement(BuiltInGameRules.Gomoku, new Position(row, 0)).Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Negative_Col_Throws(int col)
    {
        var act = () => new Position(0, col);

        act.Should()
            .Throw<InvalidMoveException>()
            .WithMessage($"*col {col}*negative*");
    }

    [Theory]
    [InlineData(15)]
    [InlineData(100)]
    public void Col_Beyond_A_Gomoku_Board_Is_Rejected_By_The_Rules(int col)
    {
        new Position(0, col).Col.Should().Be(col);
        AcceptsPlacement(BuiltInGameRules.Gomoku, new Position(0, col)).Should().BeFalse();
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
        GomokuBoards.Size.Should().Be(15);
        GomokuBoards.MaxIndex.Should().Be(14);
    }
}
