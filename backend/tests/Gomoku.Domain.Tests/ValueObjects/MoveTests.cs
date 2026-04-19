using Move = Gomoku.Domain.ValueObjects.Move;

namespace Gomoku.Domain.Tests.ValueObjects;

public class MoveTests
{
    [Theory]
    [InlineData(Stone.Black)]
    [InlineData(Stone.White)]
    public void Valid_Stone_Constructs_Successfully(Stone stone)
    {
        var pos = new Position(7, 7);

        var move = new Move(pos, stone);

        move.Position.Should().Be(pos);
        move.Stone.Should().Be(stone);
    }

    [Fact]
    public void Empty_Stone_Throws()
    {
        var pos = new Position(7, 7);
        var act = () => new Move(pos, Stone.Empty);

        act.Should()
            .Throw<InvalidMoveException>()
            .WithMessage("*Stone.Empty*");
    }

    [Fact]
    public void Equal_Moves_Are_Value_Equal()
    {
        var a = new Move(new Position(3, 4), Stone.Black);
        var b = new Move(new Position(3, 4), Stone.Black);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }
}
