using System.Collections.Generic;
using System.Linq;
using Gewu.Domain.Games.Cards;

namespace Gewu.Domain.Games.Wakeng;

/// <summary>
/// 挖坑的**大小**与**连续性**,两件事分开。
/// <para>
/// 挖坑的顺序是 <c>3 &gt; 2 &gt; A &gt; K &gt; Q &gt; J &gt; 10 &gt; 9 &gt; 8 &gt; 7 &gt; 6 &gt; 5 &gt; 4</c> ——
/// **3 最大而不是最小**。`CardRank` 的数值是**编码**顺序(它与斗地主的大小顺序恰好一致),
/// 所以挖坑 MUST 自己映一层,MUST NOT 直接拿 <c>(int)rank</c> 比大小。
/// </para>
/// </summary>
public static class WakengRank
{
    /// <summary>
    /// 一张牌在挖坑里的强弱。数值越大越强:4 = 1,……,A = 11,2 = 12,3 = 13。
    /// <para>
    /// 王不参与 —— 挖坑用 52 张,牌堆里没有王。真传进来一张王,它会拿到 0,
    /// 比任何一张牌都小;但那种输入应该在解码那一层就被拦下,而不是靠这里兜着。
    /// </para>
    /// </summary>
    /// <param name="rank">点数。</param>
    public static int Strength(CardRank rank) => rank switch
    {
        CardRank.Three => 13,
        CardRank.Two => 12,
        CardRank.SmallJoker or CardRank.BigJoker => 0,
        _ => (int)rank - 3,
    };

    /// <summary>这张牌在挖坑里的强弱。</summary>
    /// <param name="card">牌。</param>
    public static int Strength(Card card) => Strength(card.Rank);

    /// <summary>
    /// 可以组成**连牌**(顺子 / 连对 / 飞机 / 火箭)的点数:**4 到 K**。
    /// <para>
    /// 原文只排除了 3 和 2,却又说「因此连到 K 的顺子是相同张数中最大的」—— 而 A 在挖坑的
    /// 大小表里比 K 大,所以那个「因此」只有在 **A 也不能进连牌**时才成立。用户按后者定的。
    /// </para>
    /// <para>
    /// **它是一处判断,不是推导**,所以只有一份:改这一处就同时改掉四种连牌,而不是四处各改一次。
    /// 用户答的是「A 不能进顺子」,而把同一条用到连对 / 飞机 / 火箭上是**我的推断** ——
    /// 那三种用的是同一个序列,分开处理会让「445566 合法而 456 不合法」这种自相矛盾成为可能。
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<CardRank> RunnableRanks =
        Enumerable.Range((int)CardRank.Four, (int)CardRank.King - (int)CardRank.Four + 1)
            .Select(x => (CardRank)x)
            .ToList();

    /// <summary>这个点数能不能进连牌。</summary>
    /// <param name="rank">点数。</param>
    public static bool CanRun(CardRank rank) => rank is >= CardRank.Four and <= CardRank.King;

    /// <summary>
    /// 连牌里的位置,4 = 1 到 K = 10;不能进连牌的点数返回 <c>null</c>。
    /// <para>
    /// 它与 <see cref="Strength"/> 在 4–K 上是同一个数,而**分成两个函数是故意的**:
    /// 强弱要覆盖 13 个点数,连续性只覆盖 10 个,而把它们合成一个会让「A 算第 11 位」
    /// 这种错悄悄成立。
    /// </para>
    /// </summary>
    /// <param name="rank">点数。</param>
    public static int? RunIndex(CardRank rank) => CanRun(rank) ? (int)rank - 3 : null;
}
