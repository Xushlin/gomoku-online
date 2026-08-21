using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Doudizhu;

namespace Gewu.Domain.Tests.Games.Doudizhu;

/// <summary>
/// 斗地主候选出法的枚举。
/// <para>
/// 它与挖坑那一份是**两份实现**,而这个文件里的断言就是理由:跨型压(炸弹)与带填充牌的
/// 六种牌型,挖坑一个都没有。
/// </para>
/// </summary>
public class DoudizhuFollowsTests
{
    private static List<Card> Hand(params string[] cards) => cards.Select(Parse).ToList();

    private static Card Parse(string text)
    {
        if (text == "小") return new Card(CardRank.SmallJoker, CardSuit.None);
        if (text == "大") return new Card(CardRank.BigJoker, CardSuit.None);
        var rank = text[..^1] switch
        {
            "J" => CardRank.Jack,
            "Q" => CardRank.Queen,
            "K" => CardRank.King,
            "A" => CardRank.Ace,
            "2" => CardRank.Two,
            var n => (CardRank)int.Parse(n),
        };
        // 与 add-wakeng-cards 里那个 helper 同一条:`CardRank.Two` 的值是 15,
        // 而 `(CardRank)int.Parse("2")` 会造出一个未定义的枚举值。
        System.Enum.IsDefined(rank).Should().BeTrue($"'{text}' 解出的点数必须是定义过的");
        var suit = text[^1] switch
        {
            'c' => CardSuit.Clubs,
            'd' => CardSuit.Diamonds,
            'h' => CardSuit.Hearts,
            _ => CardSuit.Spades,
        };
        return new Card(rank, suit);
    }

    private static CardCombo Combo(params string[] cards)
    {
        var combo = CardCombo.Recognise(Hand(cards));
        combo.Should().NotBeNull($"'{string.Join(" ", cards)}' 必须是一个合法牌型");
        return combo!.Value;
    }

    private static IReadOnlyList<IReadOnlyList<Card>> Follows(
        IReadOnlyList<Card> hand, CardCombo? table)
        => DoudizhuFollows.For(hand, table);

    // ---- 跨型压:炸弹 ---------------------------------------------------

    [Fact]
    public void A_bomb_follows_any_non_bomb()
    {
        // **这是与挖坑最大的那一处差别。** 挖坑没有炸弹,所以它的候选只在一种牌型之内;
        // 这里一个四张同点数压得住桌上任何非炸弹的牌型。
        var hand = Hand("7c", "7d", "7h", "7s");

        var follows = Follows(hand, Combo("Kc"));

        follows.Should().ContainSingle();
        CardCombo.Recognise(follows[0])!.Value.Kind.Should().Be(ComboKind.Bomb);
    }

    [Fact]
    public void A_rocket_follows_a_bomb_but_a_smaller_bomb_does_not()
    {
        var withRocket = Hand("小", "大");
        var withSmallBomb = Hand("4c", "4d", "4h", "4s");

        Follows(withRocket, Combo("Kc", "Kd", "Kh", "Ks")).Should().ContainSingle();
        Follows(withSmallBomb, Combo("Kc", "Kd", "Kh", "Ks")).Should().BeEmpty();
    }

    [Fact]
    public void When_the_table_is_a_bomb_no_plain_shape_is_offered()
    {
        var hand = Hand("Ac", "Ad", "Ah", "2c", "2d", "3c");

        var follows = Follows(hand, Combo("4c", "4d", "4h", "4s"));

        follows.Should().BeEmpty("桌上是炸弹,而这手牌里没有更大的炸弹也没有王炸");
    }

    [Fact]
    public void A_quad_with_kickers_is_not_a_bomb()
    {
        // **四带二不是炸弹** —— 它只压得住更小的四带二,而不是任何四张的牌型。
        // 这条与 add-doudizhu-cards 里那条同源,而那一条曾经因为「张数几乎从不相同」
        // 而在四头真变成炸弹时照样是绿的。这里是候选层面的同一个检查。
        var hand = Hand("7c", "7d", "7h", "7s", "3c", "3d");

        // 桌上是一个更小的四带两单:4444 + 5 6。
        var follows = Follows(hand, Combo("4c", "4d", "4h", "4s", "5c", "6c"));

        // 7777 + 33 是四带两对,压不住四带两单(牌型不同);而 7777 本身是炸弹,压得住。
        var kinds = follows.Select(f => CardCombo.Recognise(f)!.Value.Kind).ToList();
        kinds.Should().Contain(ComboKind.Bomb);
        kinds.Should().NotContain(ComboKind.QuadWithPairs);
    }

    // ---- 带牌:只列一条 -------------------------------------------------

    [Fact]
    public void One_triplet_yields_exactly_one_triplet_with_single()
    {
        // **这条是「只列一条」那处判断的可执行形式。** 一个三条配五张不同的单牌,
        // 全列出来会是五条候选 —— 而它们只有填充牌不同,`Beats` 只看那个三条。
        var hand = Hand("9c", "9d", "9h", "4c", "5c", "6c", "Jc", "Qc");

        var follows = Follows(hand, Combo("8c", "8d", "8h", "3c"));

        var withSingle = follows
            .Where(f => CardCombo.Recognise(f)!.Value.Kind == ComboKind.TripletWithSingle)
            .ToList();

        withSingle.Should().ContainSingle("一个三条只给一条三带一,而不是三条数 × 单张数");
        // 而它带的是最弱的那一张 —— 填充牌是要扔掉的东西。
        withSingle[0].Should().Contain(Parse("4c"));
    }

    [Fact]
    public void The_kicker_is_the_weakest_card_available()
    {
        var hand = Hand("9c", "9d", "9h", "3c", "Ac");

        var follows = Follows(hand, Combo("8c", "8d", "8h", "4c"));
        var withSingle = follows
            .Single(f => CardCombo.Recognise(f)!.Value.Kind == ComboKind.TripletWithSingle);

        withSingle.Should().Contain(Parse("3c"));
        withSingle.Should().NotContain(Parse("Ac"), "A 不该被当成填充牌扔掉");
    }

    [Fact]
    public void A_triplet_with_nothing_to_spare_offers_no_kicker_shape()
    {
        // 配不齐填充牌就不产生候选 —— 而不是产生一个非法的三带一。
        var hand = Hand("9c", "9d", "9h");

        var follows = Follows(hand, Combo("8c", "8d", "8h", "3c"));

        follows.Should().BeEmpty("手上只有三张,配不出三带一");
    }

    // ---- 自由首出与顺子 -------------------------------------------------

    [Fact]
    public void Leading_offers_a_straight_of_five_but_not_of_four()
    {
        var hand = Hand("4c", "5c", "6c", "7c", "8c");

        var follows = Follows(hand, null);

        var straights = follows
            .Where(f => CardCombo.Recognise(f)!.Value.Kind == ComboKind.Straight)
            .ToList();
        straights.Should().ContainSingle("单顺最少 5 张,所以这手牌只有一个顺子");
        straights[0].Should().HaveCount(5);
    }

    [Fact]
    public void A_longer_straight_never_follows_a_shorter_one()
    {
        // **顺子只与同长度的比** —— 这是斗地主最容易实现错的一条。
        var hand = Hand("5c", "6c", "7c", "8c", "9c", "10c");

        var follows = Follows(hand, Combo("4c", "5d", "6d", "7d", "8d"));

        foreach (var play in follows.Where(f => CardCombo.Recognise(f)!.Value.Kind == ComboKind.Straight))
        {
            play.Should().HaveCount(5, "六张顺子压不住五张顺子");
        }
    }

    // ---- 两个出口一致 ---------------------------------------------------

    [Fact]
    public void Cannot_follow_needs_a_hand_with_no_bomb_or_it_proves_nothing()
    {
        // **这条前提是规格明写的。** 手里有炸弹就永远出得起,所以一个没有这条前提的
        // 「要不起」测试会因为**别的理由**通过 —— 而那正是本仓库反复栽的那个形状。
        var hand = Hand("3c", "4c", "5c");

        hand.GroupBy(c => c.Rank).Should().OnlyContain(g => g.Count() < 4, "手里 MUST 没有炸弹");
        hand.Count(c => c.IsJoker).Should().BeLessThan(2, "也 MUST 没有王炸");

        Follows(hand, Combo("Kc")).Should().BeEmpty();
        Follows(hand, Combo("3d")).Should().NotBeEmpty("而同一手牌压得住一张 3");
    }

    [Fact]
    public void CanFollow_agrees_with_the_list_on_every_position()
    {
        var hand = Hand("3c", "4c", "5c", "9c", "9d", "9h", "Ac");
        CardCombo?[] tables =
        [
            null,
            Combo("3d"),
            Combo("2c"),
            Combo("8c", "8d", "8h"),
            Combo("4c", "4d", "4h", "4s"),
            Combo("5c", "6c", "7c", "8c", "9s"),
        ];

        var sawBoth = new HashSet<bool>();
        foreach (var table in tables)
        {
            var expected = Follows(hand, table).Count > 0;
            DoudizhuFollows.CanFollow(hand, table).Should().Be(expected, $"table={table}");
            sawBoth.Add(expected);
        }

        sawBoth.Should().BeEquivalentTo(new[] { true, false },
            "两种答案都要出现过 —— 只走一边的遍历什么都不验");
    }

    [Fact]
    public void Every_offered_play_is_legal_held_and_actually_beats_the_table()
    {
        var hand = Hand("3c", "3d", "4c", "4d", "5c", "5d", "9c", "9d", "9h", "Kc", "小", "大");
        var table = Combo("8c", "8d");

        var follows = Follows(hand, table);

        follows.Should().NotBeEmpty();
        foreach (var play in follows)
        {
            var combo = CardCombo.Recognise(play);
            combo.Should().NotBeNull($"{Card.Encode(play)} 必须是合法牌型");
            combo!.Value.Beats(table).Should().BeTrue($"{Card.Encode(play)} 必须真的压得住");
            play.Should().OnlyContain(c => hand.Contains(c), "而且必须真在手上");
            play.Distinct().Should().HaveCount(play.Count, "同一张牌不能用两次");
        }
    }

    [Fact]
    public void Bombs_are_offered_last()
    {
        // 提示从最弱的一手开始。一个炸弹比任何非炸弹都强,而它的张数可能更少 ——
        // 所以排序先按层,否则提示的第一手就是炸弹。
        var hand = Hand("3c", "4c", "9c", "9d", "9h", "9s");

        var follows = Follows(hand, Combo("3d"));

        var kinds = follows.Select(f => CardCombo.Recognise(f)!.Value.Kind).ToList();
        kinds.Should().NotBeEmpty();
        kinds[^1].Should().Be(ComboKind.Bomb, "炸弹排在最后");
        kinds[0].Should().NotBe(ComboKind.Bomb);
    }
}
