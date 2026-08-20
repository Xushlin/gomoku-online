using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Doudizhu;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.Doudizhu;

/// <summary>
/// 斗地主的规则接在内核上之后的行为:阶段、叫分、出牌、过牌、超时兜底。
/// <para>
/// 每个用例都从一段**历史**出发,因为规则就是这么工作的 —— 它无状态,每次 <c>Apply</c> 从
/// <c>(Setup, History)</c> 重建局面。
/// </para>
/// </summary>
public class DoudizhuRulesTests
{
    private const int Seed = 20260819;

    private static readonly DoudizhuRules Rules = new();

    private static readonly string Setup = Rules.CreateSetup(Seed);

    private static readonly DoudizhuDeal Deal = DoudizhuDeal.Decode(Setup);

    private static MatchState State(params (int Seat, string Text)[] history)
        => new(Setup, [.. history.Select(h => PlayedMove.Said(h.Text, h.Seat))]);

    private static MoveApplication Apply(MatchState state, int seat, string text)
        => Rules.Apply(state, MoveIntent.Say(text), seat);

    // ---- 身份 -----------------------------------------------------------

    [Fact]
    public void It_declares_three_seats_human_play_and_no_rating()
    {
        Rules.GameKey.Should().Be("doudizhu");
        Rules.SeatCount.Should().Be(3);
        Rules.SupportsHumanVsHuman.Should().BeTrue();

        // ELO 是两人模型,而斗地主按分结算 —— 一个按分的阶梯是另一条榜。
        // 这也让 `IsRated ⇒ SeatCount == 2` 那条不变量不需要为斗地主开例外。
        Rules.IsRated.Should().BeFalse();
    }

    [Fact]
    public void The_setup_is_a_deal_and_the_same_seed_deals_the_same_cards()
    {
        Rules.CreateSetup(Seed).Should().Be(Setup);
        Deal.Hands.Should().AllSatisfy(h => h.Should().HaveCount(17));
        Deal.Kitty.Should().HaveCount(3);
    }

    // ---- 叫分 -----------------------------------------------------------

    [Fact]
    public void Bidding_three_ends_the_bidding_and_the_bidder_leads()
    {
        var result = Apply(State(), 0, "bid:3");

        result.Result.Should().Be(GameResult.Ongoing);
        result.NextSeat.Should().Be(0, "叫 3 分没人压得过,他是地主而且先出牌");
    }

    [Fact]
    public void The_highest_bidder_after_three_bids_is_the_landlord()
    {
        var result = Apply(State((0, "bid:1"), (1, "bid:0")), 2, "bid:2");

        result.Result.Should().Be(GameResult.Ongoing);
        result.NextSeat.Should().Be(2);
    }

    [Fact]
    public void A_middle_bid_just_rotates()
    {
        var result = Apply(State((0, "bid:1")), 1, "bid:2");

        result.Result.Should().Be(GameResult.Ongoing);
        result.NextSeat.Should().BeNull("叫分还没结束,按环轮转");
    }

    [Fact]
    public void A_bid_that_does_not_beat_the_current_high_is_refused()
    {
        var act = () => Apply(State((0, "bid:2")), 1, "bid:2");

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Passing_the_bid_is_always_allowed()
    {
        var act = () => Apply(State((0, "bid:3".Replace("3", "2"))), 1, "bid:0");

        act.Should().NotThrow();
    }

    [Fact]
    public void Nobody_bidding_is_a_draw()
    {
        // **不重新发牌。** 重发需要在同一个 Game 上换第二份 Setup,而"发牌在开局那一刻定下、
        // 之后不变"是重放与"服务端侧设置"这个概念的地基。
        var result = Apply(State((0, "bid:0"), (1, "bid:0")), 2, "bid:0");

        result.Result.Should().Be(GameResult.Draw);
        result.WinnerSeat.Should().BeNull();
    }

    [Fact]
    public void Playing_cards_during_the_bidding_is_refused()
    {
        var card = Deal.Hands[0][0].Encode();

        var act = () => Apply(State(), 0, $"play:{card}");

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Bidding_after_the_bidding_has_ended_is_refused()
    {
        var act = () => Apply(State((0, "bid:3")), 0, "bid:1");

        act.Should().Throw<InvalidMoveException>();
    }

    // ---- 出牌 -----------------------------------------------------------

    /// <summary>座位 0 叫 3 分当地主之后的局面。</summary>
    private static MatchState AfterBidding(params (int Seat, string Text)[] plays)
        => State([(0, "bid:3"), .. plays]);

    /// <summary>地主(座位 0)手上的 20 张,按大小升序。</summary>
    private static IReadOnlyList<Card> LandlordHand()
        => DoudizhuTable.Reconstruct(AfterBidding()).HandOf(0);

    [Fact]
    public void The_landlord_holds_twenty_cards()
    {
        var table = DoudizhuTable.Reconstruct(AfterBidding());

        table.HandOf(0).Should().HaveCount(20, "17 张手牌加 3 张底牌");
        table.HandOf(1).Should().HaveCount(17);
        table.HandOf(2).Should().HaveCount(17);
        table.Landlord.Should().Be(0);
        table.BaseScore.Should().Be(3);
    }

    [Fact]
    public void The_leader_cannot_pass()
    {
        var act = () => Apply(AfterBidding(), 0, "pass");

        act.Should().Throw<InvalidMoveException>()
            .WithMessage("*lead*");
    }

    [Fact]
    public void Playing_a_card_you_do_not_hold_is_refused()
    {
        // 地主手上没有的一张 —— 从另一家手里挑。
        var notMine = DoudizhuTable.Reconstruct(AfterBidding()).HandOf(1)
            .First(c => !LandlordHand().Contains(c));

        var act = () => Apply(AfterBidding(), 0, $"play:{notMine.Encode()}");

        act.Should().Throw<InvalidMoveException>().WithMessage("*do not hold*");
    }

    [Fact]
    public void The_same_card_cannot_be_played_twice_across_turns()
    {
        var card = LandlordHand()[0].Encode();
        var after = AfterBidding((0, $"play:{card}"), (1, "pass"), (2, "pass"));

        var act = () => Apply(after, 0, $"play:{card}");

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_play_that_does_not_beat_the_table_is_refused()
    {
        var hand = LandlordHand();
        var big = hand[^1];
        var opponent = DoudizhuTable.Reconstruct(AfterBidding()).HandOf(1);
        var smaller = opponent.FirstOrDefault(c => c.Rank < big.Rank);
        smaller.Should().NotBe(default(Card), "这副牌里 1 号座位得有一张比地主最大那张小的");

        var after = AfterBidding((0, $"play:{big.Encode()}"));

        var act = () => Apply(after, 1, $"play:{smaller.Encode()}");

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void Two_passes_clear_the_table_and_the_next_lead_is_free()
    {
        var hand = LandlordHand();
        var after = AfterBidding((0, $"play:{hand[0].Encode()}"), (1, "pass"), (2, "pass"));

        var table = DoudizhuTable.Reconstruct(after);
        table.Current.Should().BeNull("两家过牌之后桌面清空");

        // 桌面空了,所以地主可以出任意合法牌型 —— 包括一张比刚才那张更小的(没有更小的就用同一大小)。
        var act = () => Apply(after, 0, $"play:{table.HandOf(0)[0].Encode()}");
        act.Should().NotThrow();

        var pass = () => Apply(after, 0, "pass");
        pass.Should().Throw<InvalidMoveException>("清空之后是首出,首出不能过牌");
    }

    [Fact]
    public void Passing_is_allowed_when_the_table_is_not_empty()
    {
        var after = AfterBidding((0, $"play:{LandlordHand()[0].Encode()}"));

        var result = Apply(after, 1, "pass");

        result.Result.Should().Be(GameResult.Ongoing);
        result.NextSeat.Should().BeNull("过牌按环轮转");
    }

    [Fact]
    public void A_positional_payload_is_refused()
    {
        var act = () => Rules.Apply(
            AfterBidding(), MoveIntent.Place(new Position(0, 0)), 0);

        act.Should().Throw<InvalidMoveException>();
    }

    // ---- 超时兜底 -------------------------------------------------------

    [Fact]
    public void The_bidding_fallback_is_a_pass()
    {
        Rules.MoveOnTimeout(State(), 0).Text.Should().Be("bid:0");
    }

    [Fact]
    public void The_play_fallback_passes_when_it_can()
    {
        var after = AfterBidding((0, $"play:{LandlordHand()[0].Encode()}"));

        Rules.MoveOnTimeout(after, 1).Text.Should().Be("pass");
    }

    [Fact]
    public void The_lead_fallback_plays_the_smallest_single()
    {
        // 首出不能过牌,所以兜底必须真出一张 —— 而单牌永远是合法牌型,所以这总是可行的。
        // **这一条是 `MoveOnTimeout` 必须看得到发牌的原因**:手牌不在历史里。
        var smallest = LandlordHand()[0];

        var intent = Rules.MoveOnTimeout(AfterBidding(), 0);

        intent.Text.Should().Be($"play:{smallest.Encode()}");
    }

    [Fact]
    public void Every_fallback_move_is_legal()
    {
        // 兜底动作会走与真人落子完全相同的路径(经过 Apply),所以它非法就等于房间卡住。
        var states = new[]
        {
            (State(), 0),
            (State((0, "bid:1")), 1),
            (AfterBidding(), 0),
            (AfterBidding((0, $"play:{LandlordHand()[0].Encode()}")), 1),
        };

        foreach (var (state, seat) in states)
        {
            var intent = Rules.MoveOnTimeout(state, seat);
            Rules.Invoking(r => r.Apply(state, intent, seat)).Should().NotThrow(
                $"seat {seat} 的兜底动作必须合法");
        }
    }
}
