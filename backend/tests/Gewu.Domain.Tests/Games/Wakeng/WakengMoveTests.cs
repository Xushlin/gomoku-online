using FluentAssertions;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Wakeng;

namespace Gewu.Domain.Tests.Games.Wakeng;

/// <summary>
/// 一步挖坑在 <c>Move.Text</c> 里的编码。
/// <para>
/// 语法与斗地主一模一样,而这是**另一个类型** —— 它们可以分歧,而「分歧是允许的」正是
/// 「这不是同一个事实」的检验。共享的只有那条 <c>FormatException</c> 映射
/// (见 <c>CardPlayTests</c>)。
/// </para>
/// </summary>
public class WakengMoveTests
{
    [Fact]
    public void Every_kind_survives_a_round_trip()
    {
        WakengMove.Parse(WakengMove.Bidding(0).Encode()).Should().Be(WakengMove.Bidding(0));
        WakengMove.Parse(WakengMove.Bidding(3).Encode()).Should().Be(WakengMove.Bidding(3));
        WakengMove.Parse(WakengMove.Passing().Encode()).Kind.Should().Be(WakengMoveKind.Pass);

        var cards = Card.DecodeMany("cab");
        var parsed = WakengMove.Parse(WakengMove.Playing(cards).Encode());
        parsed.Kind.Should().Be(WakengMoveKind.Play);
        parsed.Cards.Should().BeEquivalentTo(cards);
    }

    [Fact]
    public void The_encodings_are_the_ones_written_down()
    {
        // 这是持久化格式,所以它被逐字节钉住 —— 换一种写法就是换了一份数据库里的数据。
        WakengMove.Bidding(0).Encode().Should().Be("bid:0");
        WakengMove.Bidding(2).Encode().Should().Be("bid:2");
        WakengMove.Passing().Encode().Should().Be("pass");
        WakengMove.Playing(Card.DecodeMany("ab")).Encode().Should().Be("play:ab");
    }

    [Theory]
    [InlineData("bid:+2")]
    [InlineData("bid: 2")]
    [InlineData("bid:22")]
    [InlineData("bid:")]
    public void A_bid_must_be_exactly_one_digit(string text)
    {
        // int.TryParse 会接受 "+2" / " 2" / "2 " —— 而 Move.Text 是持久化格式,
        // 同一步棋只该有一种写法。
        var act = () => WakengMove.Parse(text);

        act.Should().Throw<InvalidMoveException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(9)]
    public void A_bid_outside_zero_to_three_is_refused(int points)
    {
        var act = () => WakengMove.Bidding(points);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_bid_of_nine_parses_as_a_bid_and_is_then_refused()
    {
        // `bid:9` 语法上是一步叫分,语义上越界。两层分开:解析认出它是叫分,
        // 而工厂拒绝那个分数 —— 于是错误消息说的是「叫分是 0–3」,而不是「这不是一步棋」。
        var act = () => WakengMove.Parse("bid:9");

        act.Should().Throw<InvalidMoveException>()
            .WithMessage("*0*3*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("cab")]
    [InlineData("Pass")]
    [InlineData("play")]
    [InlineData("bid")]
    public void A_move_without_a_tag_is_not_a_move(string text)
    {
        // **标签不是装饰。** `cab` 是一手合法的三张牌(牌的字母表是 A-Za-z@#),
        // 而没有标签就分不出「这是动作还是牌」。
        var act = () => WakengMove.Parse(text);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Pass_and_a_play_that_spells_pass_are_different_things()
    {
        // `pass` 是过牌。而 `play:pass` —— 那一串有两个 `s`,重复的牌会被拒,所以它抛。
        // 那是**运气**(那个词刚好有重复字母),不是标签的功劳:`play:paw` 是一手真牌。
        WakengMove.Parse("pass").Kind.Should().Be(WakengMoveKind.Pass);

        var repeated = () => WakengMove.Parse("play:pass");
        repeated.Should().Throw<InvalidMoveException>();

        WakengMove.Parse("play:paw").Kind.Should().Be(WakengMoveKind.Play);
        WakengMove.Parse("play:paw").Cards.Should().HaveCount(3);
    }

    [Fact]
    public void A_play_fits_in_the_move_text_column()
    {
        // 挖坑者拿完底牌是 20 张,`play:` 五个字符 —— 25,装得进 Move.Text 的 64。
        var wholeHand = Card.SuitedDeck.Take(20).ToList();

        WakengMove.Playing(wholeHand).Encode().Length.Should().BeLessThanOrEqualTo(64);
    }
}
