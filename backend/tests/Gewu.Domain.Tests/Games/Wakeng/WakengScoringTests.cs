using System;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Games.Wakeng;

namespace Gewu.Domain.Tests.Games.Wakeng;

/// <summary>
/// 挖坑的计分:叫分 × 基数,挖坑者那一侧 ×2,**三家之和恒为零**。
/// </summary>
public class WakengScoringTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(3, 5)]
    public void The_three_deltas_always_sum_to_zero(int bid, int baseScore)
    {
        // 分是在桌上转手的,不是从空气里长出来的。**两个方向都要查** ——
        // 只查一个方向的话,「挖坑者赢时多给一份」这种错有一半的时候是看不见的。
        WakengScoring.Settle(0, bid, baseScore, diggerWon: true).Sum().Should().Be(0);
        WakengScoring.Settle(0, bid, baseScore, diggerWon: false).Sum().Should().Be(0);
    }

    [Fact]
    public void The_digger_wins_double_what_each_farmer_loses()
    {
        var deltas = WakengScoring.Settle(diggerSeat: 1, bid: 2, baseScore: 1, diggerWon: true);

        deltas.Should().Equal([-2, 4, -2]);
    }

    [Fact]
    public void The_digger_loses_double_what_each_farmer_wins()
    {
        var deltas = WakengScoring.Settle(diggerSeat: 1, bid: 2, baseScore: 1, diggerWon: false);

        deltas.Should().Equal([2, -4, 2]);
    }

    [Fact]
    public void The_base_multiplies_everything()
    {
        var deltas = WakengScoring.Settle(diggerSeat: 0, bid: 3, baseScore: 10, diggerWon: true);

        deltas.Should().Equal([60, -30, -30]);
    }

    [Fact]
    public void Which_seat_digs_only_moves_the_doubled_entry()
    {
        for (var digger = 0; digger < 3; digger++)
        {
            var deltas = WakengScoring.Settle(digger, bid: 1, baseScore: 1, diggerWon: true);

            deltas[digger].Should().Be(2);
            deltas.Where((_, i) => i != digger).Should().OnlyContain(d => d == -1);
        }
    }

    [Fact]
    public void The_forced_bid_and_the_default_base_are_named_constants()
    {
        // 三家都不挖时**第一家挖,兜底 1 倍** —— 原文没写这种情况,这是用户定的一处判断。
        // 一个有名字的常量比一个写在分支里的 `1` 更难被误读成「随便取的」。
        WakengScoring.ForcedBid.Should().Be(1);
        WakengScoring.DefaultBase.Should().Be(1);
        WakengScoring.MaxBid.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void A_bid_outside_one_to_three_is_refused(int bid)
    {
        // 0 分是「不挖」,而**不挖的人不结算** —— 一个 0 分的结算会把所有人的分都算成 0,
        // 看起来像「这局没人输赢」,而那与「这局根本没算」长得一样。
        var act = () => WakengScoring.Settle(0, bid, 1, diggerWon: true);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_seat_outside_the_table_is_refused()
    {
        var act = () => WakengScoring.Settle(3, 1, 1, diggerWon: true);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_base_below_one_is_refused()
    {
        var act = () => WakengScoring.Settle(0, 1, 0, diggerWon: true);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
