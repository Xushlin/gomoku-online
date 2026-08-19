using System;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Games.Doudizhu;

namespace Gewu.Domain.Tests.Games.Doudizhu;

/// <summary>发牌必须可复盘 —— 重放一局靠的就是这一点。</summary>
public class DoudizhuDealTests
{
    private const int PinnedSeed = 20260819;

    [Fact]
    public void A_deal_is_seventeen_each_plus_three()
    {
        var deal = DoudizhuDeal.FromSeed(PinnedSeed);

        deal.Hands.Should().HaveCount(3);
        deal.Hands.Should().AllSatisfy(h => h.Should().HaveCount(17));
        deal.Kitty.Should().HaveCount(3);
    }

    [Fact]
    public void A_deal_uses_every_card_exactly_once()
    {
        var deal = DoudizhuDeal.FromSeed(PinnedSeed);

        var all = deal.Hands.SelectMany(h => h).Concat(deal.Kitty).ToList();
        all.Should().HaveCount(54);
        all.Distinct().Should().HaveCount(54, "一张牌不能同时在两个人手里");
    }

    [Fact]
    public void The_same_seed_always_deals_the_same_cards()
    {
        var a = DoudizhuDeal.FromSeed(PinnedSeed);
        var b = DoudizhuDeal.FromSeed(PinnedSeed);

        a.Encode().Should().Be(b.Encode());
    }

    [Fact]
    public void Different_seeds_deal_differently()
    {
        DoudizhuDeal.FromSeed(1).Encode().Should().NotBe(DoudizhuDeal.FromSeed(2).Encode());
    }

    [Fact]
    public void Seed_zero_is_substituted_so_the_shuffle_keeps_its_entropy()
    {
        // **这条测试是变异测试逼出来的,而它替换掉的那一条断言是错的。**
        //
        // 我原本断言"seed 0 的第一手不等于牌堆前 17 张",理由写的是"状态 0 等于根本没洗"。
        // 那个理由不对:状态恒为 0 时每次的 j 都是 0,于是每一步都跟 0 号位交换 —— 牌确实动了,
        // 所以那条断言在守卫被改坏之后照样绿。真正的后果是**熵全丢**:任何落到零状态的种子
        // 发出同一副牌。
        //
        // 所以这里钉精确的那条:seed 0 被替换成那个常数,于是它必须与直接给出该常数的种子
        // 发出同一副牌。
        var substitute = unchecked((int)0x9E3779B9);

        DoudizhuDeal.FromSeed(0).Encode().Should().Be(DoudizhuDeal.FromSeed(substitute).Encode());
        DoudizhuDeal.FromSeed(0).Encode().Should().NotBe(DoudizhuDeal.FromSeed(1).Encode());
    }

    [Fact]
    public void A_deal_round_trips_through_its_encoding()
    {
        var deal = DoudizhuDeal.FromSeed(PinnedSeed);

        var again = DoudizhuDeal.Decode(deal.Encode());

        again.Encode().Should().Be(deal.Encode());
        for (var seat = 0; seat < 3; seat++)
        {
            again.Hands[seat].Should().Equal(deal.Hands[seat]);
        }
        again.Kitty.Should().Equal(deal.Kitty);
    }

    [Fact]
    public void The_encoded_deal_is_pinned()
    {
        // 编码是持久化格式,而这一串是"给定这个种子,发牌就该是这些牌"的字节级证据。
        // 洗牌算法一改,这条就红 —— 那正是要的:历史对局的重放会跟着变。
        var encoded = DoudizhuDeal.FromSeed(PinnedSeed).Encode();

        encoded.Should().HaveLength(17 + 17 + 17 + 3 + 3, "54 张牌 + 3 个分隔符");
        encoded.Count(c => c == '/').Should().Be(3);
        // 四段各自的长度,而不是整串的内容 —— 内容由上面那条 round-trip 与
        // "54 张各一次"共同钉住,这里钉的是结构。
        encoded.Split('/').Select(s => s.Length).Should().Equal(17, 17, 17, 3);
    }

    [Fact]
    public void Decoding_refuses_a_short_hand()
    {
        var deal = DoudizhuDeal.FromSeed(PinnedSeed);
        var parts = deal.Encode().Split('/');
        var broken = string.Join("/", parts[0][..16], parts[1], parts[2], parts[3]);

        var act = () => DoudizhuDeal.Decode(broken);

        act.Should().Throw<FormatException>().WithMessage("*16 cards*");
    }

    [Fact]
    public void Decoding_refuses_the_wrong_number_of_sections()
    {
        var act = () => DoudizhuDeal.Decode("AB/CD");

        act.Should().Throw<FormatException>().WithMessage("*sections*");
    }

    [Fact]
    public void Decoding_refuses_a_deal_that_does_not_use_the_whole_deck()
    {
        // 把两家的第一张牌换成同一张 —— 段数、张数都对,但牌重复了。
        var deal = DoudizhuDeal.FromSeed(PinnedSeed);
        var parts = deal.Encode().Split('/');
        var broken = string.Join("/", parts[1][0] + parts[0][1..], parts[1], parts[2], parts[3]);

        var act = () => DoudizhuDeal.Decode(broken);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Hands_come_back_sorted()
    {
        var deal = DoudizhuDeal.FromSeed(PinnedSeed);

        // 排好序是为了让编码稳定,也是为了让 UI 不必自己排。
        foreach (var hand in deal.Hands)
        {
            hand.Should().BeInAscendingOrder();
        }
    }
}
