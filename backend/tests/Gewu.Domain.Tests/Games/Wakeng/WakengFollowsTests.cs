using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Wakeng;

namespace Gewu.Domain.Tests.Games.Wakeng;

/// <summary>
/// 候选出法的枚举 —— **「要不起」与「提示」共用的那一个事实**。
/// </summary>
public class WakengFollowsTests
{
    /// <summary>按「4c 5d Kh」这种写法造牌。</summary>
    private static List<Card> Hand(params string[] cards) => cards.Select(Parse).ToList();

    private static Card Parse(string text)
    {
        var rank = text[..^1] switch
        {
            "J" => CardRank.Jack,
            "Q" => CardRank.Queen,
            "K" => CardRank.King,
            "A" => CardRank.Ace,
            "2" => CardRank.Two,
            var n => (CardRank)int.Parse(n),
        };
        // 这一句不是防御性编程,是 add-wakeng-cards 里踩过的那个坑的可执行形式:
        // `CardRank.Two` 的值是 15,而 `(CardRank)int.Parse("2")` 会造出一个未定义的枚举值。
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

    private static WakengCombo Combo(params string[] cards)
    {
        WakengCombo.TryRecognise(Hand(cards), out var combo).Should().BeTrue();
        return combo;
    }

    // ---- 自由首出 -------------------------------------------------------

    [Fact]
    public void Leading_lists_every_shape_the_hand_can_make()
    {
        // 4 4 5:两张单(4、5)、一对(44)。**没有** 445 这种带牌的东西。
        var follows = WakengFollows.For(Hand("4c", "4d", "5c"), null);

        follows.Should().HaveCount(3);
        follows.Select(f => f.Count).Should().BeEquivalentTo(new[] { 1, 1, 2 });
        foreach (var f in follows)
        {
            WakengCombo.TryRecognise(f, out _).Should().BeTrue("每一项都必须是合法牌型");
        }
    }

    [Fact]
    public void Leading_finds_runs_of_three_or_more()
    {
        var follows = WakengFollows.For(Hand("4c", "5c", "6c", "7c"), null);

        // 四张单 + 三张顺(456、567)+ 四张顺(4567)。
        follows.Where(f => f.Count == 3).Should().HaveCount(2);
        follows.Where(f => f.Count == 4).Should().HaveCount(1);
        // **两组连牌不是牌型** —— 45 不在里面。
        follows.Where(f => f.Count == 2).Should().BeEmpty();
    }

    [Fact]
    public void A_run_never_uses_A_2_or_3()
    {
        // A / 2 / 3 不能进连牌,所以 J Q K A 里只有 JQK 一个三张顺。
        var follows = WakengFollows.For(Hand("Jc", "Qc", "Kc", "Ac"), null);

        follows.Where(f => f.Count >= 3).Should().ContainSingle()
            .Which.Should().HaveCount(3);
    }

    // ---- 跟牌 -----------------------------------------------------------

    [Fact]
    public void Following_only_offers_the_same_kind_and_the_same_count()
    {
        // 桌上是三张顺 4 5 6。手里有 7 8 9(压得住)、一个四头(压不住 —— 挖坑没有炸弹)。
        var hand = Hand("7c", "8c", "9c", "Kc", "Kd", "Kh", "Ks");

        var follows = WakengFollows.For(hand, Combo("4c", "5c", "6c"));

        follows.Should().ContainSingle("只有 789 这一手同型同张数且更大");
        follows[0].Should().HaveCount(3);
        follows[0].Select(c => c.Rank).Should().BeEquivalentTo(
            new[] { CardRank.Seven, CardRank.Eight, CardRank.Nine });
    }

    [Fact]
    public void A_quad_does_not_follow_a_same_sized_run()
    {
        // **同张数、不同牌型、而且更大** —— 那是唯一能区分「四头不是炸弹」的形状。
        var follows = WakengFollows.For(Hand("Kc", "Kd", "Kh", "Ks"), Combo("4c", "5c", "6c", "7c"));

        follows.Should().BeEmpty("四头压不住同张数的顺子");
    }

    [Fact]
    public void Cannot_follow_returns_an_empty_list_and_a_smaller_table_does_not()
    {
        // **这一条是「自动过牌」的可执行形式,而它带正面对照** ——
        // 少了对照,一个恒返回空列表的实现同样是绿的。
        var hand = Hand("4c", "5c", "6c");

        var hopeless = WakengFollows.For(hand, Combo("Kc"));
        var doable = WakengFollows.For(hand, Combo("4d"));

        hopeless.Should().BeEmpty("手里最大的单牌是 6,压不住 K");
        doable.Should().NotBeEmpty("而同一手牌压得住一张 4");
    }

    [Fact]
    public void Three_is_the_strongest_single()
    {
        // 挖坑的大小是 3 > 2 > A —— 一张 3 压得住 2,而 2 压不住 3。
        WakengFollows.For(Hand("3c"), Combo("2c")).Should().NotBeEmpty();
        WakengFollows.For(Hand("2c"), Combo("3c")).Should().BeEmpty();
    }

    // ---- 两个出口读同一个事实 -------------------------------------------

    [Fact]
    public void CanFollow_agrees_with_the_list_on_every_position()
    {
        // **两个出口读同一个事实,那就该有一条断言把它们钉在一起。**
        // 「要不起」与「提示」若各算一遍,就会出现「提示说你能出、而系统已经替你过了」。
        var hand = Hand("4c", "5c", "6c", "Kc", "Kd");
        WakengCombo?[] tables =
        [
            null,
            Combo("4d"),
            Combo("Kh"),
            Combo("3c"),
            Combo("4d", "5d", "6d"),
            Combo("Ac", "Ad"),
        ];

        var sawBoth = new HashSet<bool>();
        foreach (var table in tables)
        {
            var expected = WakengFollows.For(hand, table).Count > 0;
            WakengFollows.CanFollow(hand, table).Should().Be(expected, $"table={table}");
            sawBoth.Add(expected);
        }

        sawBoth.Should().BeEquivalentTo(new[] { true, false },
            "两种答案都要出现过 —— 只走一边的遍历什么都不验");
    }

    [Fact]
    public void Every_offered_play_is_actually_accepted_by_the_rules()
    {
        // 提示不许选中一手服务端会拒的牌。这条是那句话的可执行形式:
        // 候选逐项过 `TryRecognise` 与 `Beats`。
        var hand = Hand("4c", "4d", "5c", "5d", "6c", "6d", "Kc", "Kd", "Kh", "3c");
        var table = Combo("4h", "4s");

        var follows = WakengFollows.For(hand, table);

        follows.Should().NotBeEmpty();
        foreach (var play in follows)
        {
            WakengCombo.TryRecognise(play, out var combo).Should().BeTrue();
            combo.Beats(table).Should().BeTrue($"{Card.Encode(play)} 必须真的压得住");
            play.Should().OnlyContain(c => hand.Contains(c), "而且必须真在手上");
        }
    }

    [Fact]
    public void The_offers_are_ordered_weakest_first()
    {
        // 提示从最弱的一手开始 —— 连点是「换一手更大的」,而不是随机跳。
        var follows = WakengFollows.For(Hand("4c", "5c", "6c", "7c"), Combo("4d"));

        var strengths = follows.Select(f => f.Max(WakengRank.Strength)).ToList();
        strengths.Should().BeInAscendingOrder();
    }
}
