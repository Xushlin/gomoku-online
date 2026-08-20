using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Wakeng;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.Wakeng;

/// <summary>
/// 挖坑的规则接在内核上之后的行为:阶段、叫分、出牌、过牌、超时兜底。
/// <para>
/// 每个用例都从一段**历史**出发,因为规则就是这么工作的 —— 它无状态,每次 <c>Apply</c> 从
/// <c>(Setup, History)</c> 重建局面。
/// </para>
/// <para>
/// <b>座位号一律相对首叫者算。</b> 挖坑的首手来自发牌,所以写死「0 号先叫」的脚本会在换种子时
/// 变成非法历史 —— 而更糟的是它可能**碰巧**合法,于是断言在测别的东西。
/// </para>
/// </summary>
public class WakengRulesTests
{
    private const int Seed = 20260820;

    private static readonly WakengRules Rules = new();

    private static readonly string Setup = Rules.CreateSetup(Seed);

    private static readonly WakengDeal Deal = WakengDeal.Decode(Setup);

    /// <summary>首叫者 —— 他也首出。</summary>
    private static int First => Deal.FirstBidder().Seat;

    /// <summary>首叫者之后第 <paramref name="n"/> 家。</summary>
    private static int Seat(int n) => (First + n) % WakengDeal.SeatCount;

    private static MatchState State(params (int Seat, string Text)[] history)
        => new(Setup, [.. history.Select(h => PlayedMove.Said(h.Text, h.Seat))]);

    private static MoveApplication Apply(MatchState state, int seat, string text)
        => Rules.Apply(state, MoveIntent.Say(text), seat);

    /// <summary>三家都不挖之后的局面 —— 首叫者兜底,轮到他首出。</summary>
    private static MatchState AfterForcedBid()
        => State((Seat(0), "bid:0"), (Seat(1), "bid:0"), (Seat(2), "bid:0"));

    // ---- 身份 -----------------------------------------------------------

    [Fact]
    public void It_declares_three_seats_human_play_and_no_rating()
    {
        Rules.GameKey.Should().Be(GameKeys.Wakeng);
        Rules.SeatCount.Should().Be(3);
        Rules.SupportsHumanVsHuman.Should().BeTrue();
        Rules.IsRated.Should().BeFalse("ELO 是两人模型,而挖坑按分结算");
        Rules.Should().NotBeAssignableTo<IBoardGameRules>("挖坑没有盘面");
    }

    [Fact]
    public void It_implements_the_five_seams_it_needs()
    {
        Rules.Should().BeAssignableTo<IDealtGameRules>();
        Rules.Should().BeAssignableTo<IFirstSeatRules>();
        Rules.Should().BeAssignableTo<ITimeoutFallbackRules>();
        Rules.Should().BeAssignableTo<IPerSeatViewRules>();
    }

    [Fact]
    public void The_first_seat_is_the_holder_of_the_smallest_club()
    {
        Rules.FirstSeat(new MatchState(Setup, [])).Should().Be(First);
    }

    [Fact]
    public void The_deal_is_a_pure_function_of_the_seed()
    {
        Rules.CreateSetup(Seed).Should().Be(Setup);
    }

    // ---- 叫分 -----------------------------------------------------------

    [Fact]
    public void Only_bids_are_accepted_during_the_bidding()
    {
        var state = State();
        var hand = WakengTable.Reconstruct(state).HandOf(First);

        var play = () => Apply(state, First, $"play:{hand[0].Encode()}");
        var pass = () => Apply(state, First, "pass");

        play.Should().Throw<InvalidMoveException>();
        pass.Should().Throw<InvalidMoveException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_bid_that_does_not_beat_the_highest_is_refused(int tooLow)
    {
        var state = State((Seat(0), "bid:2"));

        var act = () => Apply(state, Seat(1), $"bid:{tooLow}");

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Zero_is_always_legal_and_three_always_beats()
    {
        var state = State((Seat(0), "bid:2"));

        Apply(state, Seat(1), "bid:0").Result.Should().Be(GameResult.Ongoing);
        Apply(state, Seat(1), "bid:3").Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void A_bid_of_three_ends_the_bidding_at_once()
    {
        var outcome = Apply(State(), First, "bid:3");

        outcome.Result.Should().Be(GameResult.Ongoing);
        outcome.NextSeat.Should().Be(First, "叫到 3 没人压得过,另两家不再被问");

        var table = WakengTable.Reconstruct(State((First, "bid:3")));
        table.Phase.Should().Be(WakengPhase.Playing);
        table.Digger.Should().Be(First);
        table.Bid.Should().Be(3);
    }

    [Fact]
    public void The_lead_goes_back_to_the_first_bidder_even_when_someone_else_digs()
    {
        // **这条是首出权那条规则的核心断言,而它需要挖坑者 ≠ 首叫者。**
        //
        // 首叫者不挖,下一家叫 3 —— 于是挖坑者是下一家,而出手权回到**首叫者**。
        // 斗地主在这里会把出手权给地主;挖坑不会。
        var outcome = Apply(State((Seat(0), "bid:0")), Seat(1), "bid:3");

        var table = WakengTable.Reconstruct(State((Seat(0), "bid:0"), (Seat(1), "bid:3")));
        table.Digger.Should().Be(Seat(1), "叫 3 的是下一家");

        outcome.NextSeat.Should().Be(First, "而首出权在首叫者手里");
        outcome.NextSeat.Should().NotBe(table.Digger, "这一局挖坑者不是首叫者 —— 断言才分得出来");
    }

    [Fact]
    public void The_named_lead_is_not_the_same_as_the_natural_rotation()
    {
        // **三家各叫一次时自然轮转恰好也落在首叫者身上,而那是一个巧合**(3 个座位、3 次叫分)。
        // 这条断言盯的是「有人叫 3」那条路径:自然轮转会给下一家,而规则必须指名首叫者。
        var outcome = Apply(State(), First, "bid:3");
        var natural = (First + 1) % WakengDeal.SeatCount;

        outcome.NextSeat.Should().Be(First);
        outcome.NextSeat.Should().NotBe(natural, "自然轮转在这条路径上会给错人");
    }

    [Fact]
    public void Three_bids_end_the_bidding_and_the_highest_digs()
    {
        var outcome = Apply(State((Seat(0), "bid:1"), (Seat(1), "bid:2")), Seat(2), "bid:0");

        outcome.Result.Should().Be(GameResult.Ongoing);
        outcome.NextSeat.Should().Be(First);

        var table = WakengTable.Reconstruct(
            State((Seat(0), "bid:1"), (Seat(1), "bid:2"), (Seat(2), "bid:0")));
        table.Digger.Should().Be(Seat(1));
        table.Bid.Should().Be(2);
    }

    [Fact]
    public void Nobody_digging_makes_the_first_bidder_dig_at_one()
    {
        // **三家都不挖时第一家挖,兜底 1 倍** —— 用户定的,而原文没写这种情况。
        // 于是**挖坑没有流局**:斗地主在同一条路径上是和局。
        var outcome = Apply(State((Seat(0), "bid:0"), (Seat(1), "bid:0")), Seat(2), "bid:0");

        outcome.Result.Should().Be(GameResult.Ongoing, "MUST NOT 是和局 —— 挖坑没有流局");
        outcome.Result.Should().NotBe(GameResult.Draw);
        outcome.NextSeat.Should().Be(First);

        var table = WakengTable.Reconstruct(AfterForcedBid());
        table.Phase.Should().Be(WakengPhase.Playing);
        table.Digger.Should().Be(First, "兜底的是首叫者");
        table.Bid.Should().Be(WakengScoring.ForcedBid);
    }

    [Fact]
    public void No_sequence_of_bids_can_draw_this_game()
    {
        // 上一条是一个例子;这一条是**穷举**:三家的每一种合法叫分组合都走一遍,
        // 而 `GameResult.Draw` MUST 一次都不出现。一个照抄斗地主流局分支的实现会在这里红。
        var walked = 0;

        foreach (var a in Enumerable.Range(0, WakengScoring.MaxBid + 1))
        {
            foreach (var b in Enumerable.Range(0, WakengScoring.MaxBid + 1))
            {
                foreach (var c in Enumerable.Range(0, WakengScoring.MaxBid + 1))
                {
                    var history = new List<(int, string)>();
                    var results = new List<GameResult>();
                    var bids = new[] { a, b, c };

                    for (var i = 0; i < bids.Length; i++)
                    {
                        var state = State([.. history]);
                        if (WakengTable.Reconstruct(state).Phase != WakengPhase.Bidding)
                        {
                            break;  // 有人叫了 3,叫分已经结束
                        }

                        MoveApplication outcome;
                        try
                        {
                            outcome = Apply(state, Seat(i), $"bid:{bids[i]}");
                        }
                        catch (InvalidMoveException)
                        {
                            break;  // 压不过当前最高 —— 这一支不是一段合法历史
                        }

                        results.Add(outcome.Result);
                        history.Add((Seat(i), $"bid:{bids[i]}"));
                    }

                    results.Should().NotContain(GameResult.Draw, $"bids {a}/{b}/{c}");
                    walked += results.Count;
                }
            }
        }

        walked.Should().BeGreaterThan(60, "穷举若走空,这条断言什么都没验");
    }

    [Fact]
    public void The_digger_takes_the_four_card_kitty()
    {
        var table = WakengTable.Reconstruct(AfterForcedBid());

        table.HandOf(table.Digger!.Value).Should().HaveCount(
            WakengDeal.HandSize + WakengDeal.KittySize, "16 + 4 = 20");
        foreach (var seat in Enumerable.Range(0, WakengDeal.SeatCount).Where(s => s != table.Digger))
        {
            table.HandOf(seat).Should().HaveCount(WakengDeal.HandSize);
        }

        table.HandOf(table.Digger!.Value).Should().Contain(table.Kitty);
    }

    [Fact]
    public void A_bid_after_the_bidding_is_refused()
    {
        var act = () => Apply(AfterForcedBid(), First, "bid:3");

        act.Should().Throw<InvalidMoveException>();
    }

    // ---- 出牌 -----------------------------------------------------------

    [Fact]
    public void The_leader_must_play_and_cannot_pass()
    {
        var act = () => Apply(AfterForcedBid(), First, "pass");

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_card_not_in_hand_is_refused()
    {
        var state = AfterForcedBid();
        var mine = WakengTable.Reconstruct(state).HandOf(First).ToHashSet();
        var notMine = Card.SuitedDeck.First(c => !mine.Contains(c));

        var act = () => Apply(state, First, $"play:{notMine.Encode()}");

        act.Should().Throw<InvalidMoveException>().WithMessage("*hold*");
    }

    [Fact]
    public void Cards_that_are_not_a_combination_are_refused()
    {
        // 挖坑没有带牌,所以「三张里两张同点」这种手是认不出来的牌型。
        var state = AfterForcedBid();
        var hand = WakengTable.Reconstruct(state).HandOf(First);
        var odd = FindUnrecognisable(hand);

        var act = () => Apply(state, First, $"play:{Card.Encode(odd)}");

        act.Should().Throw<InvalidMoveException>().WithMessage("*combination*");
    }

    [Fact]
    public void A_follow_that_does_not_beat_the_table_is_refused()
    {
        var state = AfterForcedBid();
        var table = WakengTable.Reconstruct(state);

        // 首叫者出一张**强**牌,下一家拿一张更弱的单牌去跟 —— 压不住。
        var strongest = table.HandOf(First).MaxBy(WakengRank.Strength);
        var next = Seat(1);
        var weakerFollow = table.HandOf(next)
            .Where(c => WakengRank.Strength(c) < WakengRank.Strength(strongest))
            .OrderBy(WakengRank.Strength)
            .First();

        var after = State(
            (Seat(0), "bid:0"), (Seat(1), "bid:0"), (Seat(2), "bid:0"),
            (First, $"play:{strongest.Encode()}"));

        var act = () => Apply(after, next, $"play:{weakerFollow.Encode()}");

        act.Should().Throw<InvalidMoveException>().WithMessage("*does not beat*");
    }

    [Fact]
    public void Two_passes_clear_the_table_and_the_lead_returns()
    {
        var state = AfterForcedBid();
        var lead = WakengTable.Reconstruct(state).HandOf(First).MinBy(WakengRank.Strength);

        var after = State(
            (Seat(0), "bid:0"), (Seat(1), "bid:0"), (Seat(2), "bid:0"),
            (First, $"play:{lead.Encode()}"),
            (Seat(1), "pass"),
            (Seat(2), "pass"));

        var table = WakengTable.Reconstruct(after);
        table.Current.Should().BeNull("两家过牌之后桌面清空");
        table.CurrentSeat.Should().BeNull();
        table.CurrentCards.Should().BeEmpty();

        // 桌面空了,所以首叫者可以自由首出任何合法牌型 —— 包括他最强的那张。
        var strongest = table.HandOf(First).MaxBy(WakengRank.Strength);
        Apply(after, First, $"play:{strongest.Encode()}").Result.Should().Be(GameResult.Ongoing);
    }

    // ---- 超时兜底 -------------------------------------------------------

    [Fact]
    public void A_timeout_during_the_bidding_does_not_dig()
    {
        Rules.MoveOnTimeout(State(), First).Text.Should().Be("bid:0");
    }

    [Fact]
    public void A_timeout_while_following_passes()
    {
        var state = AfterForcedBid();
        var lead = WakengTable.Reconstruct(state).HandOf(First).MinBy(WakengRank.Strength);
        var after = State(
            (Seat(0), "bid:0"), (Seat(1), "bid:0"), (Seat(2), "bid:0"),
            (First, $"play:{lead.Encode()}"));

        Rules.MoveOnTimeout(after, Seat(1)).Text.Should().Be("pass");
    }

    [Fact]
    public void A_timeout_while_leading_plays_the_weakest_card_by_wakeng_strength()
    {
        // **这条测试是一条真缺陷的可执行形式。** 手牌按 `Card` 的自然序排,而那是**编码**顺序
        // (3、4、…、K、A、2)—— 它恰好就是斗地主的大小顺序。挖坑是 `3 > 2 > A > … > 4`,
        // 于是照抄斗地主的 `HandOf(seat)[0]` 在手上有 3 的时候取到的是**最强**的一张:
        // 托管会替他把最好的牌打掉。
        var state = AfterForcedBid();
        var hand = WakengTable.Reconstruct(state).HandOf(First);

        // **前提:这手牌里必须有 3 或 2**,否则编码序的第一张恰好也是最弱的,
        // 而这条断言就分不出正确实现与照抄的实现。
        hand.Any(c => c.Rank == CardRank.Three || c.Rank == CardRank.Two).Should().BeTrue(
            "否则 HandOf[0] 与「最弱」重合,这条断言什么都不验");

        var weakest = hand.MinBy(WakengRank.Strength);
        weakest.Should().NotBe(hand[0], "编码序的第一张不是挖坑里最弱的那张");

        Rules.MoveOnTimeout(state, First).Text.Should().Be($"play:{weakest.Encode()}");
    }

    [Fact]
    public void The_fallback_move_is_always_legal()
    {
        // 兜底走的是与真人完全相同的路径,所以它 MUST 是该局面下合法的一步。
        var state = AfterForcedBid();

        var intent = Rules.MoveOnTimeout(state, First);
        var act = () => Rules.Apply(state, intent, First);

        act.Should().NotThrow();
    }

    // ---- 重建 -----------------------------------------------------------

    [Fact]
    public void A_game_with_no_deal_breaks_loudly()
    {
        var act = () => Rules.Apply(new MatchState(null, []), MoveIntent.Say("bid:0"), 0);

        act.Should().Throw<InvalidMoveException>().WithMessage("*no deal*");
    }

    [Fact]
    public void Reconstruction_is_a_pure_function()
    {
        var state = AfterForcedBid();

        var a = WakengTable.Reconstruct(state);
        var b = WakengTable.Reconstruct(state);

        a.Phase.Should().Be(b.Phase);
        a.Digger.Should().Be(b.Digger);
        a.Bid.Should().Be(b.Bid);
        for (var seat = 0; seat < WakengDeal.SeatCount; seat++)
        {
            a.HandOf(seat).Should().Equal(b.HandOf(seat));
        }
    }

    [Fact]
    public void A_move_with_coordinates_is_refused()
    {
        // 挖坑没有盘面,所以带坐标的载荷在形状校验那一步就被挡下。
        var act = () => Rules.Apply(State(), MoveIntent.Place(new Position(0, 0)), First);

        act.Should().Throw<InvalidMoveException>();
    }

    /// <summary>
    /// 从一手牌里找一组**认不出来**的牌 —— 两张同点加一张别的(三带一,挖坑不许带牌)。
    /// </summary>
    private static IReadOnlyList<Card> FindUnrecognisable(IReadOnlyList<Card> hand)
    {
        var pair = hand.GroupBy(c => c.Rank).FirstOrDefault(g => g.Count() >= 2);
        if (pair is not null)
        {
            var other = hand.First(c => c.Rank != pair.Key);
            return [.. pair.Take(2), other];
        }

        // 一手 20 张里没有一对的概率极低,但真没有的话,三张不连续的单牌同样认不出来。
        var spread = hand.Where((_, i) => i % 2 == 0).Take(3).ToList();
        return spread;
    }
}
