using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.ValueObjects;

/// <summary>
/// 「一步棋恰好携带一种载荷」这条不变量。
/// <para>
/// 这些用例**直接调主构造器**,不走 <c>Place</c> / <c>Slide</c> / <c>Say</c>。工厂是约定 ——
/// 一个 record struct 的主构造器随时能被直接调用,而这个仓库反复付过「不变量只写在文档里」
/// 的账。测试打的是机制,不是约定。
/// </para>
/// </summary>
public class MovePayloadTests
{
    private static readonly Position A = new(3, 4);
    private static readonly Position B = new(5, 6);

    [Fact]
    public void A_placement_carries_a_destination_and_nothing_else()
    {
        var intent = MoveIntent.Place(A);

        intent.From.Should().BeNull();
        intent.To.Should().Be(A);
        intent.Text.Should().BeNull();
    }

    [Fact]
    public void A_slide_carries_both_squares()
    {
        var intent = MoveIntent.Slide(A, B);

        intent.From.Should().Be(A);
        intent.To.Should().Be(B);
        intent.Text.Should().BeNull();
    }

    [Fact]
    public void A_spoken_move_carries_only_text()
    {
        var intent = MoveIntent.Say("一心一意");

        intent.From.Should().BeNull();
        intent.To.Should().BeNull();
        intent.Text.Should().Be("一心一意");
    }

    [Fact]
    public void Text_is_trimmed()
    {
        MoveIntent.Say("  一心一意 ").Text.Should().Be("一心一意");
    }

    [Fact]
    public void Both_payloads_at_once_is_refused()
    {
        var act = () => new MoveIntent(null, A, "一心一意");

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Neither_payload_is_refused()
    {
        var act = () => new MoveIntent(null, null, null);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void The_parameterless_constructor_is_refused_too()
    {
        // `default(MoveIntent)` cannot run a constructor and so cannot be blocked —
        // but `new MoveIntent()` can, and it is the one someone actually writes.
        var act = () => new MoveIntent();

        act.Should().Throw<InvalidMoveException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Blank_text_is_not_a_move(string text)
    {
        var act = () => new MoveIntent(null, null, text);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_spoken_move_cannot_have_an_origin()
    {
        var act = () => new MoveIntent(A, null, "一心一意");

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void PlayedMove_enforces_the_same_invariant()
    {
        var both = () => new PlayedMove(null, A, "一心一意", Stone.Black);
        var neither = () => new PlayedMove(null, null, null, Stone.Black);

        both.Should().Throw<InvalidMoveException>();
        neither.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_board_game_refuses_a_spoken_move_with_a_clear_error()
    {
        // The point of RequirePosition: an idiom reaching gomoku's rules produces a
        // refusal that says why, not a null dereference.
        var act = () => BuiltInGameRules.Gomoku.Apply([], MoveIntent.Say("一心一意"), Stone.Black);

        act.Should().Throw<InvalidMoveException>()
            .WithMessage("*board*");
    }

    [Fact]
    public void Every_registered_rule_that_reports_a_size_is_a_board_game()
    {
        // Walks the registry, so a game added later is covered by existing. The
        // negative half matters most: a game that fakes 0×0 would satisfy "has a
        // size" while not being a board game at all.
        foreach (var rules in BuiltInGameRules.All)
        {
            if (rules is IBoardGameRules board)
            {
                board.Rows.Should().BePositive($"{rules.GameKey} declares a board");
                board.Cols.Should().BePositive($"{rules.GameKey} declares a board");
            }
        }

        BuiltInGameRules.All.Should().Contain(r => r is IBoardGameRules);
    }
}
