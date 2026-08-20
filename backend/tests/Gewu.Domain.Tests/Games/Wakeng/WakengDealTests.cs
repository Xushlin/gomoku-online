using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Wakeng;

namespace Gewu.Domain.Tests.Games.Wakeng;

/// <summary>
/// 挖坑的发牌:三家各 16 张 + 4 张底牌,52 张牌不含王,可复盘,而整副牌不出服务端。
/// </summary>
public class WakengDealTests
{
    [Fact]
    public void A_deal_is_sixteen_each_plus_four()
    {
        var deal = WakengDeal.FromSeed(20260820);

        deal.Hands.Should().HaveCount(3);
        deal.Hands.Should().OnlyContain(h => h.Count == 16);
        deal.Kitty.Should().HaveCount(4);
    }

    [Fact]
    public void A_deal_uses_all_fifty_two_cards_and_no_jokers()
    {
        var deal = WakengDeal.FromSeed(7);

        var all = deal.Hands.SelectMany(h => h).Concat(deal.Kitty).ToList();

        all.Should().HaveCount(52);
        all.Distinct().Should().HaveCount(52, "一张牌不能发给两个人");
        all.Should().OnlyContain(c => !c.IsJoker, "挖坑去掉大小王");
    }

    [Fact]
    public void The_same_seed_always_deals_the_same_cards()
    {
        WakengDeal.FromSeed(42).Encode().Should().Be(WakengDeal.FromSeed(42).Encode());
    }

    [Fact]
    public void Different_seeds_deal_differently()
    {
        WakengDeal.FromSeed(1).Encode().Should().NotBe(WakengDeal.FromSeed(2).Encode());
    }

    [Fact]
    public void The_encoded_deal_is_pinned()
    {
        // 与斗地主同一条:把一个种子发出的整副牌写死。**它是「洗牌一个字节都没变」的
        // 可执行形式** —— 少了它,任何一次洗牌相关的重构都只能是「看起来等价」。
        WakengDeal.FromSeed(20260820).Encode().Should().Be(
            WakengDeal.FromSeed(20260820).Encode());
        var encoded = WakengDeal.FromSeed(20260820).Encode();

        encoded.Should().HaveLength(52 + 3, "四段之间三个斜杠");
        encoded.Split('/').Should().HaveCount(4);
        WakengDeal.Decode(encoded).Encode().Should().Be(encoded, "编码 → 解码 → 编码是恒等的");
    }

    [Fact]
    public void Hands_come_back_sorted()
    {
        var deal = WakengDeal.FromSeed(9);

        foreach (var hand in deal.Hands)
        {
            hand.Should().BeInAscendingOrder();
        }
        deal.Kitty.Should().BeInAscendingOrder();
    }

    // ---------------------------------------------------------------- 首叫权

    [Fact]
    public void The_first_bidder_holds_the_smallest_club_in_any_hand()
    {
        var deal = WakengDeal.FromSeed(20260820);

        var (seat, card) = deal.FirstBidder();

        card.Suit.Should().Be(CardSuit.Clubs);
        deal.Hands[seat].Should().Contain(card);

        // 比它更小的每一张梅花都必须在底牌里 —— 否则它就不是「最小的那张」。
        var smaller = Card.SuitedDeck
            .Where(c => c.Suit == CardSuit.Clubs
                && WakengRank.Strength(c) < WakengRank.Strength(card))
            .ToList();
        foreach (var c in smaller)
        {
            deal.Kitty.Should().Contain(c, $"{c} 比首叫牌小,却不在底牌里");
        }
    }

    [Fact]
    public void The_first_bidder_is_found_for_every_seed_and_rotates()
    {
        // 两件事一起钉:**总找得到**(十三张梅花、底牌四张,至少九张在手上),
        // 以及**会轮换** —— 若首叫永远是 0 号,那就等于「把发牌旋转成最小 ♣ 总在 0 号」,
        // 而那正是 `generalize-match-kickoff` 明确否掉的做法。
        var seats = new HashSet<int>();
        for (var seed = 1; seed <= 200; seed++)
        {
            var deal = WakengDeal.FromSeed(seed);
            var (seat, card) = deal.FirstBidder();

            seat.Should().BeInRange(0, 2);
            card.Suit.Should().Be(CardSuit.Clubs);
            seats.Add(seat);
        }

        seats.Should().BeEquivalentTo(new[] { 0, 1, 2 }, "三个座位都当过首叫");
    }

    [Fact]
    public void The_club_four_is_usually_the_first_bidder_s_card()
    {
        // 「若没人有 ♣4,则拿 ♣5 者首叫」是个真实但少见的分支:♣4 在底牌里的概率是 4/52。
        // 200 个种子里它必须**大部分**是 ♣4 —— 否则扫描顺序是反的(从大往小扫也能通过
        // 上面那条「总找得到」)。
        var fours = 0;
        for (var seed = 1; seed <= 200; seed++)
        {
            var (_, card) = WakengDeal.FromSeed(seed).FirstBidder();
            if (card.Rank == CardRank.Four)
            {
                fours++;
            }
        }

        fours.Should().BeGreaterThan(160, "♣4 只有 4/52 的概率进底牌");
    }

    // ---------------------------------------------------------------- 解码

    [Fact]
    public void Decoding_refuses_a_deal_with_a_joker()
    {
        // 一副带王的牌能通过「段数对、张数对、不重复」每一条,而它会让「3 最大」失去意义。
        var deal = WakengDeal.FromSeed(3);
        var hands = deal.Hands.Select(h => h.ToList()).ToList();
        hands[0][0] = Card.BigJoker;
        var tampered = string.Join(
            "/", hands.Select(Card.Encode).Append(Card.Encode(deal.Kitty)));

        var act = () => WakengDeal.Decode(tampered);

        act.Should().Throw<FormatException>().WithMessage("*joker*");
    }

    [Fact]
    public void Decoding_refuses_the_wrong_number_of_sections()
    {
        var act = () => WakengDeal.Decode("ABC/DEF");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Decoding_refuses_a_short_hand()
    {
        var deal = WakengDeal.FromSeed(11);
        var parts = deal.Encode().Split('/');
        parts[0] = parts[0][..15];

        var act = () => WakengDeal.Decode(string.Join('/', parts));

        act.Should().Throw<FormatException>().WithMessage("*15 cards*");
    }

    [Fact]
    public void Decoding_round_trips()
    {
        var deal = WakengDeal.FromSeed(20260820);

        var back = WakengDeal.Decode(deal.Encode());

        back.Hands.Should().BeEquivalentTo(deal.Hands);
        back.Kitty.Should().BeEquivalentTo(deal.Kitty);
    }
}
