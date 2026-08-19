using System;
using FluentAssertions;
using Gewu.Domain.Games.Doudizhu;

namespace Gewu.Domain.Tests.Games.Doudizhu;

/// <summary>计分。三人得分之和恒为 0 —— 这套算法唯一能自我检查的性质。</summary>
public class DoudizhuScoringTests
{
    private static DoudizhuOutcome Plain(int baseScore, bool landlordWon) =>
        new(baseScore, Bombs: 0, Rockets: 0, Spring: false, AntiSpring: false, landlordWon);

    [Fact]
    public void The_landlord_wins_two_shares_and_each_peasant_loses_one()
    {
        var settled = DoudizhuScoring.Settle(Plain(2, landlordWon: true));

        settled.Landlord.Should().Be(4);
        settled.PerPeasant.Should().Be(-2);
        settled.Multiplier.Should().Be(1);
    }

    [Fact]
    public void The_landlord_losing_is_the_same_numbers_with_the_sign_flipped()
    {
        var settled = DoudizhuScoring.Settle(Plain(2, landlordWon: false));

        settled.Landlord.Should().Be(-4);
        settled.PerPeasant.Should().Be(2);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(1, false)]
    [InlineData(3, false)]
    public void The_three_seats_always_sum_to_zero(int baseScore, bool landlordWon)
    {
        var settled = DoudizhuScoring.Settle(Plain(baseScore, landlordWon));

        (settled.Landlord + 2 * settled.PerPeasant).Should().Be(0);
    }

    [Fact]
    public void Each_bomb_doubles()
    {
        var one = DoudizhuScoring.Settle(Plain(1, true) with { Bombs = 1 });
        var three = DoudizhuScoring.Settle(Plain(1, true) with { Bombs = 3 });

        one.Multiplier.Should().Be(2);
        three.Multiplier.Should().Be(8);
        three.Landlord.Should().Be(16);
    }

    [Fact]
    public void A_rocket_doubles_the_same_as_a_bomb()
    {
        // 家规:王炸 ×2,与普通炸弹一致 —— 刻意不给它单独的倍率,少一个特例。
        var bomb = DoudizhuScoring.Settle(Plain(1, true) with { Bombs = 1 });
        var rocket = DoudizhuScoring.Settle(Plain(1, true) with { Rockets = 1 });

        rocket.Multiplier.Should().Be(bomb.Multiplier);
    }

    [Fact]
    public void A_spring_doubles()
    {
        var settled = DoudizhuScoring.Settle(Plain(1, true) with { Spring = true });

        settled.Multiplier.Should().Be(2);
    }

    [Fact]
    public void An_anti_spring_doubles()
    {
        var settled = DoudizhuScoring.Settle(Plain(1, false) with { AntiSpring = true });

        settled.Multiplier.Should().Be(2);
        settled.Landlord.Should().Be(-4);
    }

    [Fact]
    public void Multipliers_stack_multiplicatively()
    {
        var settled = DoudizhuScoring.Settle(
            Plain(3, true) with { Bombs = 2, Rockets = 1, Spring = true });

        // 2 个炸弹 × 王炸 × 春天 = 2^4 = 16;底分 3 → 分值 48;地主 +96。
        settled.Multiplier.Should().Be(16);
        settled.Landlord.Should().Be(96);
        settled.PerPeasant.Should().Be(-48);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void The_base_score_must_come_from_the_bidding(int baseScore)
    {
        var act = () => DoudizhuScoring.Settle(Plain(baseScore, true));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void A_hand_cannot_be_both_a_spring_and_an_anti_spring()
    {
        // 春天要求地主出完牌,反春天要求农民赢 —— 不可能同时成立。与其在计分里挑一个,
        // 不如让构造出这种输入的地方当场坏掉。
        var act = () => DoudizhuScoring.Settle(
            Plain(1, true) with { Spring = true, AntiSpring = true });

        act.Should().Throw<ArgumentException>().WithMessage("*cannot be both*");
    }

    [Fact]
    public void Negative_bomb_counts_are_refused()
    {
        var act = () => DoudizhuScoring.Settle(Plain(1, true) with { Bombs = -1 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
