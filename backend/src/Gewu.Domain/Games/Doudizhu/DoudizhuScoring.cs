using System;

namespace Gewu.Domain.Games.Doudizhu;

/// <summary>一局斗地主的结算输入。</summary>
/// <param name="BaseScore">底分 —— 叫地主时的最高分,1 / 2 / 3。</param>
/// <param name="Bombs">本局出现的炸弹数(不含王炸)。</param>
/// <param name="Rockets">本局出现的王炸数。</param>
/// <param name="Spring">春天:地主出完了牌,而两名农民一张都没出过。</param>
/// <param name="AntiSpring">反春天:地主只出过首出那一手,之后一次没上,然后农民赢。</param>
/// <param name="LandlordWon">地主方是否获胜。</param>
public readonly record struct DoudizhuOutcome(
    int BaseScore,
    int Bombs,
    int Rockets,
    bool Spring,
    bool AntiSpring,
    bool LandlordWon);

/// <summary>一局斗地主的结算结果:三个座位各得多少分。</summary>
/// <param name="Landlord">地主的得分,可负。</param>
/// <param name="PerPeasant">**每一名**农民的得分,可负。</param>
/// <param name="Multiplier">倍数,便于展示"为什么是这个分"。</param>
public readonly record struct DoudizhuSettlement(int Landlord, int PerPeasant, int Multiplier);

/// <summary>
/// 斗地主的计分。
/// <para>
/// 分值 = 底分 × 倍数;地主赢拿 <c>+2×分值</c>、两名农民各 <c>−分值</c>,反之相反。
/// **三人得分之和恒为 0** —— 这条直接写成了断言,因为它是这套算法唯一能自我检查的性质。
/// </para>
/// </summary>
public static class DoudizhuScoring
{
    /// <summary>底分的合法范围 —— 叫分制,1 到 3。</summary>
    public const int MinBaseScore = 1;

    /// <summary>见 <see cref="MinBaseScore"/>。</summary>
    public const int MaxBaseScore = 3;

    /// <summary>结算一局。</summary>
    /// <param name="outcome">本局的结算输入。</param>
    /// <exception cref="ArgumentOutOfRangeException">底分越界,或炸弹 / 王炸数为负。</exception>
    /// <exception cref="ArgumentException">同一局同时是春天与反春天。</exception>
    public static DoudizhuSettlement Settle(DoudizhuOutcome outcome)
    {
        if (outcome.BaseScore is < MinBaseScore or > MaxBaseScore)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome), outcome.BaseScore,
                $"The base score comes from the bidding and must be {MinBaseScore}–{MaxBaseScore}.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(outcome.Bombs, nameof(outcome));
        ArgumentOutOfRangeException.ThrowIfNegative(outcome.Rockets, nameof(outcome));

        if (outcome.Spring && outcome.AntiSpring)
        {
            // 春天要求地主出完牌,反春天要求农民赢 —— 两者不可能同时成立。
            // 与其在计分里挑一个,不如让构造出这种输入的地方当场坏掉。
            throw new ArgumentException(
                "A hand cannot be both a spring and an anti-spring: one needs the landlord to go out, the other needs a peasant to.",
                nameof(outcome));
        }

        // 逐项翻倍(相乘)。王炸与普通炸弹都是 ×2 —— 刻意不给王炸单独的倍率,
        // 少一个特例;这条是本仓库定下的家规之一,不是通行规则。
        var multiplier = 1;
        multiplier <<= outcome.Bombs;
        multiplier <<= outcome.Rockets;
        if (outcome.Spring) multiplier <<= 1;
        if (outcome.AntiSpring) multiplier <<= 1;

        var value = outcome.BaseScore * multiplier;
        var landlord = outcome.LandlordWon ? 2 * value : -2 * value;
        var perPeasant = outcome.LandlordWon ? -value : value;

        return new DoudizhuSettlement(landlord, perPeasant, multiplier);
    }
}
