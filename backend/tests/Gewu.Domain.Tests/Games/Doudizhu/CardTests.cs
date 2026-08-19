using System;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Games.Doudizhu;

namespace Gewu.Domain.Tests.Games.Doudizhu;

/// <summary>牌与它的一字符编码。**编码是持久化格式**,所以这里钉的是字节,不只是行为。</summary>
public class CardTests
{
    [Fact]
    public void A_deck_is_fifty_four_distinct_cards()
    {
        Card.FullDeck.Should().HaveCount(54);
        Card.FullDeck.Distinct().Should().HaveCount(54);
        Card.FullDeck.Count(c => c.IsJoker).Should().Be(2);
    }

    [Fact]
    public void Every_card_round_trips_through_its_character()
    {
        foreach (var card in Card.FullDeck)
        {
            Card.Decode(card.Encode()).Should().Be(card, $"{card} must survive a round trip");
        }
    }

    [Fact]
    public void Every_card_gets_a_distinct_character()
    {
        Card.FullDeck.Select(c => c.Encode()).Distinct().Should().HaveCount(54);
    }

    [Fact]
    public void The_alphabet_is_pinned_because_it_is_a_persisted_format()
    {
        // 改一个字符,所有历史对局的重放都会读出别的牌 —— 所以这几个是钉死的,
        // 不是"当前实现恰好如此"。
        new Card(CardRank.Three, CardSuit.Clubs).Encode().Should().Be('A');
        new Card(CardRank.Three, CardSuit.Spades).Encode().Should().Be('D');
        new Card(CardRank.Four, CardSuit.Clubs).Encode().Should().Be('E');
        new Card(CardRank.Two, CardSuit.Spades).Encode().Should().Be('z');
        Card.SmallJoker.Encode().Should().Be('@');
        Card.BigJoker.Encode().Should().Be('#');
    }

    [Fact]
    public void The_alphabet_avoids_characters_that_get_escaped()
    {
        // 引号、逗号、反斜杠会在日志、CSV、JSON 里各被转义一次 —— 一个持久化格式不该
        // 需要读它的人先想清楚转义了几层。
        var forbidden = new[] { '"', '\'', ',', '\\', '/', '\n', '\r' };
        var used = Card.FullDeck.Select(c => c.Encode()).ToHashSet();
        used.Should().NotIntersectWith(forbidden);
    }

    [Fact]
    public void A_whole_hand_fits_in_the_text_payload()
    {
        // 一手最多打出 20 张(地主的全部手牌),而 Move.Text 的上限是 64 字符。
        // **这就是斗地主不需要第四种载荷的全部理由。**
        var wholeHand = Card.FullDeck.Take(20);

        Card.Encode(wholeHand).Length.Should().Be(20);
        Card.Encode(wholeHand).Length.Should().BeLessThan(64);
    }

    [Fact]
    public void Encoding_a_hand_is_order_independent()
    {
        var hand = new[]
        {
            new Card(CardRank.King, CardSuit.Hearts),
            new Card(CardRank.Three, CardSuit.Clubs),
            Card.BigJoker,
        };

        // 同一手牌只有一种写法 —— 否则"这两手是不是同一手"就得靠调用方排序。
        Card.Encode(hand).Should().Be(Card.Encode(hand.Reverse()));
    }

    [Fact]
    public void A_card_cannot_appear_twice_in_one_hand()
    {
        var act = () => Card.DecodeMany("AA");

        // 一副牌里每张只有一张,所以重复不是"非法的一手",而是**编码本身坏了**。
        // 在这里拦下,规则层就不必再想"这手牌是不是自己重复了"。
        act.Should().Throw<FormatException>().WithMessage("*twice*");
    }

    [Fact]
    public void A_character_outside_the_alphabet_is_refused()
    {
        var act = () => Card.Decode('!');

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Rank_order_is_doudizhu_order_not_poker_order()
    {
        // 2 比 A 大,王比 2 大 —— 而 A 不能当 1 用。
        ((int)CardRank.Two).Should().BeGreaterThan((int)CardRank.Ace);
        ((int)CardRank.SmallJoker).Should().BeGreaterThan((int)CardRank.Two);
        ((int)CardRank.BigJoker).Should().BeGreaterThan((int)CardRank.SmallJoker);
    }

    [Fact]
    public void Suits_do_not_affect_ordering_of_different_ranks()
    {
        var lowSpade = new Card(CardRank.Three, CardSuit.Spades);
        var highClub = new Card(CardRank.Four, CardSuit.Clubs);

        lowSpade.CompareTo(highClub).Should().BeNegative("花色不参与点数比较");
    }
}
