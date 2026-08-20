using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Wakeng;

namespace Gewu.Domain.Tests.Games.Wakeng;

/// <summary>
/// 挖坑的牌型与压牌。
/// <para>
/// **挖坑与斗地主的差别不是「多几个牌型」,而是几乎每一条都不同** —— 3 最大、没有炸弹、
/// 三条四条不能带牌、A / 2 / 3 不进连牌。所以这一组测试里最重要的几条都是**否定**的:
/// 四头压不住三头、三带一认不出来、含 A 的顺子认不出来。
/// </para>
/// </summary>
public class WakengComboTests
{
    private static IReadOnlyList<Card> Hand(params string[] cards) =>
        cards.Select(Parse).ToList();

    /// <summary>`"4c"` = ♣4。花色只影响编码,不影响任何比较。</summary>
    private static Card Parse(string text)
    {
        var suit = text[^1] switch
        {
            'c' => CardSuit.Clubs,
            'd' => CardSuit.Diamonds,
            'h' => CardSuit.Hearts,
            _ => CardSuit.Spades,
        };
        var rankText = text[..^1];
        var rank = rankText switch
        {
            "J" => CardRank.Jack,
            "Q" => CardRank.Queen,
            "K" => CardRank.King,
            "A" => CardRank.Ace,
            // **`"2"` 必须显式列出来,而这是这个 helper 第一次跑就抓到的。**
            // `CardRank` 的数值是**编码**顺序,而 2 在那个顺序里排在 A 之后 —— 它的值是 15,
            // 不是 2。写成 `(CardRank)int.Parse("2")` 得到的是一个**未定义的枚举值**,
            // 而 `Strength` 的兜底分支会给它 −1:于是「2 压得住 A」这条断言红了。
            // 我在 `hoist-card-model` 里刚写下「数值是编码顺序而不是大小顺序」,几分钟后
            // 自己踩进同一个坑 —— 所以下面那句 `IsDefined` 是必要的,而不是防御性编程。
            "2" => CardRank.Two,
            _ => (CardRank)int.Parse(rankText),
        };
        Enum.IsDefined(rank).Should().BeTrue($"'{rankText}' 不是一个点数");
        return new Card(rank, suit);
    }

    private static WakengCombo Recognise(params string[] cards)
    {
        WakengCombo.TryRecognise(Hand(cards), out var combo).Should().BeTrue();
        return combo;
    }

    private static void NotACombo(params string[] cards) =>
        WakengCombo.TryRecognise(Hand(cards), out _).Should().BeFalse();

    // ---------------------------------------------------------------- 大小

    [Fact]
    public void Three_is_the_strongest_rank_and_four_the_weakest()
    {
        // 挖坑的顺序是 3 > 2 > A > K > … > 4。**这不是扑克的顺序,也不是斗地主的顺序。**
        var ordered = new[]
        {
            CardRank.Four, CardRank.Five, CardRank.Six, CardRank.Seven, CardRank.Eight,
            CardRank.Nine, CardRank.Ten, CardRank.Jack, CardRank.Queen, CardRank.King,
            CardRank.Ace, CardRank.Two, CardRank.Three,
        };

        var strengths = ordered.Select(WakengRank.Strength).ToList();

        strengths.Should().BeInAscendingOrder();
        strengths.Should().OnlyHaveUniqueItems();
        WakengRank.Strength(CardRank.Three).Should().BeGreaterThan(WakengRank.Strength(CardRank.Two));
        WakengRank.Strength(CardRank.Two).Should().BeGreaterThan(WakengRank.Strength(CardRank.Ace));
    }

    [Fact]
    public void A_two_and_three_cannot_run()
    {
        // A 不能进连牌是**用户定的一处判断**:原文只排除 3 和 2,却又说「因此连到 K 的顺子
        // 是最大的」—— 而 A 比 K 大,那个「因此」只有在 A 也不能进连牌时才成立。
        WakengRank.CanRun(CardRank.King).Should().BeTrue();
        WakengRank.CanRun(CardRank.Ace).Should().BeFalse();
        WakengRank.CanRun(CardRank.Two).Should().BeFalse();
        WakengRank.CanRun(CardRank.Three).Should().BeFalse();
        WakengRank.RunnableRanks.Should().HaveCount(10);
    }

    // ---------------------------------------------------------------- 认牌型

    [Fact]
    public void Single_pair_triple_and_quad_are_recognised_by_group_size()
    {
        Recognise("4c").Kind.Should().Be(WakengComboKind.Single);
        Recognise("4c", "4d").Kind.Should().Be(WakengComboKind.Pair);
        Recognise("4c", "4d", "4h").Kind.Should().Be(WakengComboKind.Triple);
        Recognise("4c", "4d", "4h", "4s").Kind.Should().Be(WakengComboKind.Quad);
    }

    [Fact]
    public void Runs_need_three_groups_and_are_recognised_by_group_size()
    {
        Recognise("4c", "5c", "6c").Kind.Should().Be(WakengComboKind.Straight);
        Recognise("4c", "4d", "5c", "5d", "6c", "6d").Kind.Should().Be(WakengComboKind.PairRun);
        Recognise("4c", "4d", "4h", "5c", "5d", "5h", "6c", "6d", "6h")
            .Kind.Should().Be(WakengComboKind.TripleRun);
        Recognise(
            "4c", "4d", "4h", "4s", "5c", "5d", "5h", "5s", "6c", "6d", "6h", "6s")
            .Kind.Should().Be(WakengComboKind.QuadRun);
    }

    [Fact]
    public void Two_consecutive_groups_are_not_a_run()
    {
        // 「连牌 3 组起」的直接后果 —— 而不是一条特例。
        NotACombo("4c", "5c");
        NotACombo("4c", "4d", "5c", "5d");
        NotACombo("4c", "4d", "4h", "5c", "5d", "5h");
    }

    [Fact]
    public void A_triple_cannot_carry_anything()
    {
        // 斗地主的三带一在挖坑里**不是牌型**。这条与「四头不是炸弹」一起,是这个棋种最容易
        // 被从斗地主抄错的两处。
        NotACombo("4c", "4d", "4h", "5c");
        NotACombo("4c", "4d", "4h", "5c", "5d");
    }

    [Fact]
    public void A_quad_cannot_carry_anything_either()
    {
        NotACombo("4c", "4d", "4h", "4s", "5c");
        NotACombo("4c", "4d", "4h", "4s", "5c", "5d");
    }

    [Fact]
    public void A_run_containing_ace_two_or_three_is_not_a_combo()
    {
        NotACombo("Jc", "Qc", "Kc", "Ac");
        NotACombo("Ac", "2c", "3c");
        NotACombo("Kc", "Ac", "2c");
        // 而连到 K 的那一条是合法的,并且是同张数里最大的。
        Recognise("10c", "Jc", "Qc", "Kc").Kind.Should().Be(WakengComboKind.Straight);
    }

    [Fact]
    public void A_gap_breaks_a_run()
    {
        NotACombo("4c", "5c", "7c");
    }

    [Fact]
    public void Mixed_group_sizes_are_not_a_combo()
    {
        NotACombo("4c", "4d", "4h", "5c", "5d", "6c");
    }

    [Fact]
    public void An_empty_hand_is_not_a_combo()
    {
        WakengCombo.TryRecognise([], out _).Should().BeFalse();
    }

    // ---------------------------------------------------------------- 压牌

    [Fact]
    public void A_bigger_single_beats_a_smaller_one_and_three_beats_everything()
    {
        Recognise("5c").Beats(Recognise("4c")).Should().BeTrue();
        Recognise("4c").Beats(Recognise("5c")).Should().BeFalse();
        Recognise("3c").Beats(Recognise("2c")).Should().BeTrue();
        Recognise("2c").Beats(Recognise("Ac")).Should().BeTrue();
    }

    [Fact]
    public void A_quad_is_not_a_bomb()
    {
        // **挖坑没有炸弹。** 四头只压得住更小的四头。
        var quad = Recognise("4c", "4d", "4h", "4s");

        quad.Beats(Recognise("Kc", "Kd", "Kh")).Should().BeFalse("四头压不住三头");
        quad.Beats(Recognise("Kc", "Kd")).Should().BeFalse("四头压不住对牌");
        quad.Beats(Recognise("Kc")).Should().BeFalse("四头压不住单牌");
        quad.Beats(Recognise("Kc", "Kd", "Kh", "Ks")).Should().BeFalse("小的四头压不住大的");
        Recognise("Kc", "Kd", "Kh", "Ks").Beats(quad).Should().BeTrue("大的四头压得住小的");
    }

    [Fact]
    public void A_quad_does_not_beat_a_same_sized_run_or_group_of_another_kind()
    {
        // **上面那条测试在「四头真变成炸弹」的变异下照样是绿的**,而这一条是它缺的那半。
        //
        // 原因:去掉「同型」这个条件之后,`Beats` 还剩「同张数 + 更大」——而四头与它想压的
        // 东西**张数几乎从不相同**(4 对 3、4 对 2、4 对 1),于是每一条断言都因为**别的理由**
        // 通过。唯一能区分的形状是:**同张数、不同牌型、而且四头更大。**
        //
        // 所以这里的 KKKK 是有意挑的:它和 4-5-6-7 都是四张,而 K 比 7 大。
        Recognise("Kc", "Kd", "Kh", "Ks")
            .Beats(Recognise("4c", "5c", "6c", "7c"))
            .Should().BeFalse("同是四张,但四头不是顺子 —— 挖坑没有炸弹");

        Recognise("Kc", "Kd", "Kh")
            .Beats(Recognise("4c", "5c", "6c"))
            .Should().BeFalse("同是三张,但三头不是顺子");

        Recognise("Kc", "Kd", "Kh", "Ks", "Qc", "Qd", "Qh", "Qs", "Jc", "Jd", "Jh", "Js")
            .Beats(Recognise(
                "4c", "4d", "5c", "5d", "6c", "6d", "7c", "7d", "8c", "8d", "9c", "9d"))
            .Should().BeFalse("同是十二张,但火箭不是连对");
    }

    [Fact]
    public void A_longer_run_does_not_beat_a_shorter_one()
    {
        // 跟牌必须**同张数**。五张顺子不是「更大的三张顺子」,它是另一手牌。
        var five = Recognise("4c", "5c", "6c", "7c", "8c");
        var three = Recognise("9c", "10c", "Jc");

        five.Beats(three).Should().BeFalse();
        three.Beats(five).Should().BeFalse();
    }

    [Fact]
    public void Runs_of_the_same_length_compare_by_their_top_card()
    {
        Recognise("5c", "6c", "7c").Beats(Recognise("4c", "5c", "6c")).Should().BeTrue();
        Recognise("4c", "5c", "6c").Beats(Recognise("5c", "6c", "7c")).Should().BeFalse();
    }

    [Fact]
    public void Nothing_beats_itself()
    {
        var pair = Recognise("7c", "7d");

        pair.Beats(pair).Should().BeFalse("同型同张同大小 —— 压不住");
    }

    [Fact]
    public void Suits_never_decide_anything()
    {
        // 两张同点数的牌完全等价 —— 花色只影响编码与显示。
        Recognise("7s").Beats(Recognise("7c")).Should().BeFalse();
        Recognise("7c").Beats(Recognise("7s")).Should().BeFalse();
    }
}
