using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Doudizhu;

namespace Gewu.Domain.Tests.Games.Doudizhu;

/// <summary>牌型识别与压牌。这是斗地主最容易实现错的一块。</summary>
public class CardComboTests
{
    /// <summary>按点数取 n 张不同花色的牌。</summary>
    private static IEnumerable<Card> Of(CardRank rank, int count) =>
        Card.Suits.Take(count).Select(s => new Card(rank, s));

    private static List<Card> Hand(params (CardRank Rank, int Count)[] groups) =>
        groups.SelectMany(g => Of(g.Rank, g.Count)).ToList();

    private static CardCombo Recognised(params (CardRank Rank, int Count)[] groups)
    {
        var combo = CardCombo.Recognise(Hand(groups));
        combo.Should().NotBeNull($"{string.Join("+", groups.Select(g => $"{g.Count}x{g.Rank}"))} should be legal");
        return combo!.Value;
    }

    private static CardCombo? Rejected(params (CardRank Rank, int Count)[] groups) =>
        CardCombo.Recognise(Hand(groups));

    // ---------------------------------------------------------------- 基本牌型

    [Fact]
    public void Singles_pairs_and_triplets()
    {
        Recognised((CardRank.Seven, 1)).Should().Be(new CardCombo(ComboKind.Single, CardRank.Seven, 1));
        Recognised((CardRank.Seven, 2)).Should().Be(new CardCombo(ComboKind.Pair, CardRank.Seven, 1));
        Recognised((CardRank.Seven, 3)).Should().Be(new CardCombo(ComboKind.Triplet, CardRank.Seven, 1));
    }

    [Fact]
    public void Two_jokers_are_a_rocket_not_a_pair()
    {
        var combo = CardCombo.Recognise([Card.SmallJoker, Card.BigJoker]);

        // 两张王同点数吗?不同 —— 而即使有人把它们当"一对王",规则里也没有这个牌型。
        combo.Should().Be(new CardCombo(ComboKind.Rocket, CardRank.BigJoker, 1));
    }

    [Fact]
    public void A_triplet_can_carry_a_single_or_a_pair()
    {
        Recognised((CardRank.Nine, 3), (CardRank.Four, 1))
            .Should().Be(new CardCombo(ComboKind.TripletWithSingle, CardRank.Nine, 1));
        Recognised((CardRank.Nine, 3), (CardRank.Four, 2))
            .Should().Be(new CardCombo(ComboKind.TripletWithPair, CardRank.Nine, 1));
    }

    [Fact]
    public void A_triplet_compares_by_the_triplet_not_the_kicker()
    {
        var lowTripletHighKicker = Recognised((CardRank.Four, 3), (CardRank.Ace, 1));
        var highTripletLowKicker = Recognised((CardRank.Five, 3), (CardRank.Three, 1));

        // 实现里最常见的错:拿最大的那张牌当比较依据。
        highTripletLowKicker.Beats(lowTripletHighKicker).Should().BeTrue();
        lowTripletHighKicker.Beats(highTripletLowKicker).Should().BeFalse();
    }

    // ------------------------------------------------------------------ 顺子类

    [Fact]
    public void A_straight_needs_five_cards()
    {
        Recognised((CardRank.Three, 1), (CardRank.Four, 1), (CardRank.Five, 1),
                   (CardRank.Six, 1), (CardRank.Seven, 1))
            .Should().Be(new CardCombo(ComboKind.Straight, CardRank.Seven, 5));

        Rejected((CardRank.Three, 1), (CardRank.Four, 1), (CardRank.Five, 1), (CardRank.Six, 1))
            .Should().BeNull("四张连牌不是顺子");
    }

    [Fact]
    public void A_straight_stops_at_the_ace()
    {
        // 2 和王进不了顺子 —— 这是那条"连续段范围是 3..A"的可执行形式。
        Rejected((CardRank.Ten, 1), (CardRank.Jack, 1), (CardRank.Queen, 1),
                 (CardRank.King, 1), (CardRank.Ace, 1), (CardRank.Two, 1))
            .Should().BeNull("2 不能进顺子");

        var withJoker = Hand((CardRank.Ten, 1), (CardRank.Jack, 1), (CardRank.Queen, 1),
                             (CardRank.King, 1));
        withJoker.Add(Card.SmallJoker);
        CardCombo.Recognise(withJoker).Should().BeNull("王不能进顺子");
    }

    [Fact]
    public void The_longest_legal_straight_is_twelve()
    {
        var run = Card.SuitedRanks
            .Where(r => r <= CardRank.Ace)
            .Select(r => new Card(r, CardSuit.Clubs))
            .ToList();

        run.Should().HaveCount(12, "3 到 A 共 12 个点数");
        CardCombo.Recognise(run).Should().Be(new CardCombo(ComboKind.Straight, CardRank.Ace, 12));
    }

    [Fact]
    public void Straights_only_compare_against_the_same_length()
    {
        var five = Recognised((CardRank.Three, 1), (CardRank.Four, 1), (CardRank.Five, 1),
                              (CardRank.Six, 1), (CardRank.Seven, 1));
        var sixHigher = Recognised((CardRank.Four, 1), (CardRank.Five, 1), (CardRank.Six, 1),
                                   (CardRank.Seven, 1), (CardRank.Eight, 1), (CardRank.Nine, 1));

        // 更长、更大,但压不了 —— 长度必须相同。
        sixHigher.Beats(five).Should().BeFalse();
        five.Beats(sixHigher).Should().BeFalse();
    }

    [Fact]
    public void Pair_straights_need_three_pairs()
    {
        Recognised((CardRank.Five, 2), (CardRank.Six, 2), (CardRank.Seven, 2))
            .Should().Be(new CardCombo(ComboKind.PairStraight, CardRank.Seven, 3));

        Rejected((CardRank.Five, 2), (CardRank.Six, 2)).Should().BeNull("两组连对不够");
    }

    [Fact]
    public void Airplanes_need_two_triplets()
    {
        Recognised((CardRank.Eight, 3), (CardRank.Nine, 3))
            .Should().Be(new CardCombo(ComboKind.Airplane, CardRank.Nine, 2));

        Rejected((CardRank.Eight, 3), (CardRank.Ten, 3)).Should().BeNull("三张不连续");
    }

    [Fact]
    public void Airplanes_can_carry_singles_or_pairs_but_not_a_mix()
    {
        Recognised((CardRank.Eight, 3), (CardRank.Nine, 3), (CardRank.Three, 1), (CardRank.Four, 1))
            .Should().Be(new CardCombo(ComboKind.AirplaneWithSingles, CardRank.Nine, 2));

        Recognised((CardRank.Eight, 3), (CardRank.Nine, 3), (CardRank.Three, 2), (CardRank.Four, 2))
            .Should().Be(new CardCombo(ComboKind.AirplaneWithPairs, CardRank.Nine, 2));

        // 一单一对:9 张,既不是 4m 也不是 5m。
        Rejected((CardRank.Eight, 3), (CardRank.Nine, 3), (CardRank.Three, 1), (CardRank.Four, 2))
            .Should().BeNull("翅膀不能一单一对混着");
    }

    // ---------------------------------------------------------------- 四张相关

    [Fact]
    public void Four_of_a_kind_is_a_bomb()
    {
        Recognised((CardRank.Six, 4)).Should().Be(new CardCombo(ComboKind.Bomb, CardRank.Six, 1));
    }

    [Fact]
    public void A_quad_can_carry_two_singles_or_two_pairs()
    {
        Recognised((CardRank.Six, 4), (CardRank.Three, 1), (CardRank.Four, 1))
            .Should().Be(new CardCombo(ComboKind.QuadWithSingles, CardRank.Six, 1));

        Recognised((CardRank.Six, 4), (CardRank.Three, 2), (CardRank.Four, 2))
            .Should().Be(new CardCombo(ComboKind.QuadWithPairs, CardRank.Six, 1));
    }

    [Fact]
    public void The_two_singles_a_quad_carries_may_be_the_same_rank()
    {
        // 8.3-附 a:张数决定牌型,不看是否成对。
        Recognised((CardRank.Six, 4), (CardRank.Three, 2))
            .Should().Be(new CardCombo(ComboKind.QuadWithSingles, CardRank.Six, 1));
    }

    [Fact]
    public void A_quad_with_two_is_not_a_bomb()
    {
        var quadWithTwo = Recognised((CardRank.Six, 4), (CardRank.Three, 1), (CardRank.Four, 1));
        var triplet = Recognised((CardRank.Three, 3));
        var smallBomb = Recognised((CardRank.Three, 4));

        // 三条互相独立的断言,而三条都错过是最常见的实现:
        quadWithTwo.IsBombLike.Should().BeFalse();
        quadWithTwo.Beats(triplet).Should().BeFalse("四带二压不了别的牌型");
        smallBomb.Beats(quadWithTwo).Should().BeTrue("哪怕点数更小,炸弹也压得过四带二");
    }

    [Fact]
    public void Wings_must_not_be_taken_from_a_bomb()
    {
        // 8.6 与 8.3-附 b 是**同一条规则**:任何带牌 / 翅膀不能取自一个四张同点数的组合。
        // 888 999 + 7777 —— 十张,想当"飞机带两对",但那两"对"是一个炸弹拆的。
        Rejected((CardRank.Eight, 3), (CardRank.Nine, 3), (CardRank.Seven, 4))
            .Should().BeNull("翅膀不能拆炸弹");
    }

    [Fact]
    public void Four_triplets_cannot_carry_a_bomb_as_their_four_singles()
    {
        // **变异测试在这一条上教了我一件事。** 我原本以为这条规则由飞机分支里一个
        // `WingsAreLegal` 守卫执行,于是加了这条测试去碰它。结果那条变异**还是绿的** ——
        // 因为那个守卫是死代码:含恰好一个四张的手牌一定先走到四带二分支并在那里被拒,
        // 到不了飞机分支。守卫已删,规则的实际执行点写在了那个分支上。
        //
        // 这条测试留着,而且它现在钉的是那个执行点:16 张 = 4 组连续三张 + 一个炸弹当四张单翅膀。
        Rejected((CardRank.Eight, 3), (CardRank.Nine, 3), (CardRank.Ten, 3), (CardRank.Jack, 3),
                 (CardRank.Seven, 4))
            .Should().BeNull("四张单翅膀不能是一个炸弹拆的");
    }

    [Fact]
    public void A_quad_carrying_another_quad_is_refused()
    {
        // 同一条规则的另一面:6666 + 3333 是八张,想当"四带两对"。
        Rejected((CardRank.Six, 4), (CardRank.Three, 4)).Should().BeNull();
    }

    // -------------------------------------------------------------------- 压牌

    [Fact]
    public void A_bomb_beats_anything_that_is_not_a_bomb()
    {
        var bomb = Recognised((CardRank.Three, 4));
        var longStraight = Recognised(
            (CardRank.Five, 1), (CardRank.Six, 1), (CardRank.Seven, 1),
            (CardRank.Eight, 1), (CardRank.Nine, 1), (CardRank.Ten, 1));

        // 张数不同也压得过 —— 那是炸弹的特权,不是通则。
        bomb.Beats(longStraight).Should().BeTrue();
        longStraight.Beats(bomb).Should().BeFalse();
    }

    [Fact]
    public void Bombs_compare_by_rank()
    {
        var low = Recognised((CardRank.Three, 4));
        var high = Recognised((CardRank.King, 4));

        high.Beats(low).Should().BeTrue();
        low.Beats(high).Should().BeFalse();
    }

    [Fact]
    public void A_rocket_beats_every_bomb()
    {
        var rocket = CardCombo.Recognise([Card.SmallJoker, Card.BigJoker])!.Value;
        var biggestBomb = Recognised((CardRank.Two, 4));

        rocket.Beats(biggestBomb).Should().BeTrue();
        biggestBomb.Beats(rocket).Should().BeFalse();
        rocket.Beats(rocket).Should().BeFalse("同样的王炸压不过王炸");
    }

    [Fact]
    public void Nothing_beats_itself()
    {
        // 一手牌压不过自己 —— 否则"必须严格更大"就没落实。
        var samples = new[]
        {
            Recognised((CardRank.Seven, 1)),
            Recognised((CardRank.Seven, 2)),
            Recognised((CardRank.Seven, 3), (CardRank.Four, 1)),
            Recognised((CardRank.Three, 1), (CardRank.Four, 1), (CardRank.Five, 1),
                       (CardRank.Six, 1), (CardRank.Seven, 1)),
            Recognised((CardRank.Six, 4)),
        };

        foreach (var combo in samples)
        {
            combo.Beats(combo).Should().BeFalse($"{combo.Kind} must not beat itself");
        }
    }

    [Fact]
    public void Different_kinds_of_the_same_size_do_not_compare()
    {
        var tripletWithSingle = Recognised((CardRank.Nine, 3), (CardRank.Four, 1));
        var bombLikeSize = Recognised((CardRank.Four, 4));

        // 都是四张,但一个是三带一、一个是炸弹 —— 而反过来不成立(炸弹压得过)。
        tripletWithSingle.Beats(bombLikeSize).Should().BeFalse();
        bombLikeSize.Beats(tripletWithSingle).Should().BeTrue();
    }

    [Fact]
    public void Garbage_is_refused()
    {
        Rejected((CardRank.Three, 1), (CardRank.Nine, 1)).Should().BeNull("两张不相干的单牌");
        Rejected((CardRank.Three, 2), (CardRank.Nine, 1)).Should().BeNull("一对加一张散牌");
        CardCombo.Recognise([]).Should().BeNull("空的一手不是牌型");
    }

    [Fact]
    public void Every_five_card_window_from_three_to_ace_is_a_straight()
    {
        // 穷举而不是抽样:上界与下界最容易写错一位。
        var lowest = (int)CardRank.Three;
        var highest = (int)CardRank.Ace;
        var windows = 0;

        for (var start = lowest; start + 4 <= highest; start++)
        {
            var cards = Enumerable.Range(start, 5)
                .Select(r => new Card((CardRank)r, CardSuit.Clubs))
                .ToList();

            CardCombo.Recognise(cards)
                .Should().Be(new CardCombo(ComboKind.Straight, (CardRank)(start + 4), 5));
            windows++;
        }

        windows.Should().Be(8, "3..A 上共有 8 个五张窗口");
    }
}
