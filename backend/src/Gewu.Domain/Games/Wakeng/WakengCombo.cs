using System.Collections.Generic;
using System.Linq;
using Gewu.Domain.Games.Cards;

namespace Gewu.Domain.Games.Wakeng;

/// <summary>挖坑的八种牌型。</summary>
public enum WakengComboKind
{
    /// <summary>单牌。</summary>
    Single,

    /// <summary>对牌。</summary>
    Pair,

    /// <summary>三头 —— **不能带牌**。</summary>
    Triple,

    /// <summary>四头 —— **不是炸弹,也不能带牌**。它只压得住更小的四头。</summary>
    Quad,

    /// <summary>顺子,3 张起。</summary>
    Straight,

    /// <summary>连对(拖拉机),3 对起。</summary>
    PairRun,

    /// <summary>连三头(飞机),3 组起。</summary>
    TripleRun,

    /// <summary>连四头(火箭),3 组起。</summary>
    QuadRun,
}

/// <summary>
/// 一手已经认出来的挖坑牌型。
/// <para>
/// **挖坑的牌型模型比斗地主简单得多,而这不是巧合:它没有带牌、也没有炸弹。**
/// 于是每一手合法牌都是同一句话 —— **k 组等大的牌,k &gt; 1 时点数连续**:
/// </para>
/// <list type="bullet">
///   <item><description>k = 1:单 / 对 / 三头 / 四头(按组的大小)</description></item>
///   <item><description>k ≥ 3:顺子 / 连对 / 飞机 / 火箭(按组的大小)</description></item>
/// </list>
/// <para>
/// k = 2 不是任何牌型 —— 两组连续的牌(比如 3344)在挖坑里不能出。这条不是特例,
/// 而是「连牌 3 组起」的直接后果。
/// </para>
/// <para>
/// 斗地主那边要单独处理三带一、四带二、飞机带翅膀,还要判「翅膀不能拆炸弹」;
/// 这里一条规则覆盖八种牌型。**同一个家族里,牌可以共享,规则不行** —— 而这次是规则更简单
/// 的那一边。
/// </para>
/// </summary>
/// <param name="Kind">牌型。</param>
/// <param name="Groups">几组(单张算 1 组;顺子 5 张算 5 组)。</param>
/// <param name="GroupSize">每组几张(1 / 2 / 3 / 4)。</param>
/// <param name="TopStrength">最大那一组的强弱,用于比大小。</param>
public readonly record struct WakengCombo(
    WakengComboKind Kind,
    int Groups,
    int GroupSize,
    int TopStrength)
{
    /// <summary>这手牌一共几张。</summary>
    public int CardCount => Groups * GroupSize;

    /// <summary>
    /// 认一手牌。认不出来返回 <c>false</c>。
    /// <para>
    /// 认不出来的例子:空手、三带一(挖坑不许带牌)、两组连牌(连牌 3 组起)、
    /// 含 A / 2 / 3 的连牌(见 <see cref="WakengRank.RunnableRanks"/>)、组大小不一致
    /// (比如 333 44)。
    /// </para>
    /// </summary>
    /// <param name="cards">要出的牌。</param>
    /// <param name="combo">认出来的牌型。</param>
    public static bool TryRecognise(IReadOnlyList<Card> cards, out WakengCombo combo)
    {
        combo = default;
        if (cards is null || cards.Count == 0)
        {
            return false;
        }

        var groups = cards
            .GroupBy(c => c.Rank)
            .Select(g => (Rank: g.Key, Count: g.Count()))
            .OrderBy(g => WakengRank.Strength(g.Rank))
            .ToList();

        // 每组张数必须一致 —— 挖坑没有带牌,所以 333 44 不是任何牌型。
        var size = groups[0].Count;
        if (groups.Any(g => g.Count != size) || size is < 1 or > 4)
        {
            return false;
        }

        var top = WakengRank.Strength(groups[^1].Rank);

        if (groups.Count == 1)
        {
            var kind = size switch
            {
                1 => WakengComboKind.Single,
                2 => WakengComboKind.Pair,
                3 => WakengComboKind.Triple,
                _ => WakengComboKind.Quad,
            };
            combo = new WakengCombo(kind, 1, size, top);
            return true;
        }

        // 连牌:3 组起,点数连续,而且每一个点数都能进连牌(A / 2 / 3 不行)。
        if (groups.Count < 3)
        {
            return false;
        }
        var indices = new List<int>(groups.Count);
        foreach (var (rank, _) in groups)
        {
            if (WakengRank.RunIndex(rank) is not int index)
            {
                return false;
            }
            indices.Add(index);
        }
        indices.Sort();
        for (var i = 1; i < indices.Count; i++)
        {
            if (indices[i] != indices[i - 1] + 1)
            {
                return false;
            }
        }

        var runKind = size switch
        {
            1 => WakengComboKind.Straight,
            2 => WakengComboKind.PairRun,
            3 => WakengComboKind.TripleRun,
            _ => WakengComboKind.QuadRun,
        };
        combo = new WakengCombo(runKind, groups.Count, size, top);
        return true;
    }

    /// <summary>
    /// 这手牌压不压得住 <paramref name="other"/>。
    /// <para>
    /// **必须同型、同张数、更大。** 挖坑**没有炸弹** —— 四头也压不住三头,更压不住顺子。
    /// 这三个条件缺一不可,而它们各有一条断言:少了「同型」,四头就能压对牌;
    /// 少了「同张数」,五张顺子就能压三张顺子;少了「更大」,同型同张就能互压。
    /// </para>
    /// </summary>
    /// <param name="other">桌上那一手。</param>
    public bool Beats(WakengCombo other) =>
        Kind == other.Kind
        && CardCount == other.CardCount
        && TopStrength > other.TopStrength;
}
