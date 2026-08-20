using System;
using System.Collections.Generic;
using System.Linq;

namespace Gewu.Domain.Games.Wakeng;

/// <summary>
/// 挖坑的计分:**叫分 × 基数**,挖坑者那一侧 ×2。
/// <para>
/// 原文:「挖坑者先出完,则挖坑者获胜,赢得积分为:叫分 × 基数 × 2,其他两人每人输分为:
/// 叫分 × 基数。联手两人中任意一人先出完,则两人获胜,每人赢得积分为:叫分 × 基数,
/// 挖坑者输分为:叫分 × 基数 × 2。」
/// </para>
/// <para>
/// **三人之和恒为零** —— 与斗地主同一条,理由也同一条:分是在桌上转手的,不是从空气里长出来的。
/// 一条断言把它钉住,而不是靠读公式。
/// </para>
/// </summary>
public static class WakengScoring
{
    /// <summary>叫分的上限 —— 3 分。叫到 3 分直接拿底牌,不用等另两家。</summary>
    public const int MaxBid = 3;

    /// <summary>
    /// 三家都说不挖时的兜底叫分。
    /// <para>
    /// 原文没写这种情况。用户定的是:**第一家挖,兜底 1 倍** —— 不是重新发牌。
    /// **它是一处判断,不是推导**,所以它有名字:一个叫 <c>ForcedBid</c> 的常量比一个写在
    /// 分支里的 <c>1</c> 更难被误读成「随便取的」。
    /// </para>
    /// </summary>
    public const int ForcedBid = 1;

    /// <summary>基数的默认值。将来做成房间设置时,这里是那个设置的默认。</summary>
    public const int DefaultBase = 1;

    /// <summary>
    /// 结算。返回按座位号的分数变化,**三项之和为零**。
    /// </summary>
    /// <param name="diggerSeat">挖坑者的座位号。</param>
    /// <param name="bid">叫分(1–3,或三家都不挖时的 <see cref="ForcedBid"/>)。</param>
    /// <param name="baseScore">基数。</param>
    /// <param name="diggerWon">挖坑者是不是先出完的那个人。</param>
    /// <param name="seatCount">座位数,挖坑固定 3。</param>
    /// <exception cref="ArgumentOutOfRangeException">座位号越界,或叫分 / 基数不是正数。</exception>
    public static IReadOnlyList<int> Settle(
        int diggerSeat,
        int bid,
        int baseScore,
        bool diggerWon,
        int seatCount = WakengDeal.SeatCount)
    {
        if (seatCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(seatCount), seatCount, "至少两个座位。");
        }
        if (diggerSeat < 0 || diggerSeat >= seatCount)
        {
            throw new ArgumentOutOfRangeException(nameof(diggerSeat), diggerSeat, "座位号越界。");
        }
        if (bid < 1 || bid > MaxBid)
        {
            throw new ArgumentOutOfRangeException(nameof(bid), bid, $"叫分是 1 到 {MaxBid}。");
        }
        if (baseScore < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(baseScore), baseScore, "基数至少 1。");
        }

        var unit = bid * baseScore;
        var others = seatCount - 1;

        // 挖坑者一个人对另外两个人,所以他那一侧的数是「另一侧的人数」倍 —— 三家时正好是 ×2。
        // 写成 `others` 而不是字面 2,是为了让「三人之和为零」在座位数变化时仍然成立;
        // 挖坑只有三个人,但一个凑巧等于人数的字面量会让下一个人以为那是个魔数。
        var deltas = new int[seatCount];
        for (var seat = 0; seat < seatCount; seat++)
        {
            if (seat == diggerSeat)
            {
                deltas[seat] = diggerWon ? unit * others : -unit * others;
            }
            else
            {
                deltas[seat] = diggerWon ? -unit : unit;
            }
        }

        return deltas;
    }
}
