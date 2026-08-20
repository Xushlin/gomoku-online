using System.Collections.Generic;
using System.Linq;
using Gewu.Domain.Games.Cards;

namespace Gewu.Domain.Games.Wakeng;

/// <summary>
/// 一手牌在当前局面下**全部**合法的出法。
/// <para>
/// <b>它是两件事的唯一判据:「要不起」与「提示」。</b> 「要不起」就是这个列表为空;
/// 「提示」就是在这个列表里轮换。写成两套逻辑会造出一个能自相矛盾的组合 ——
/// 提示说「你可以出这手」,而自动过牌已经替你过了。**一个事实两个读者,不是两个事实。**
/// </para>
/// <para>
/// <b>它只能在服务端。</b> 牌型识别与压牌比大小是这一局唯一的判据,客户端再写一遍就是一份
/// 会悄悄分叉的第二真源,而分叉在玩家眼里是「这游戏有 bug」。客户端拿到的是
/// **服务端算出来的事实**,不是它自己的推断。
/// </para>
/// <para>
/// <b>枚举之所以只有几十行,是因为挖坑没有带牌、也没有炸弹。</b> 每一手合法牌都是同一句话
/// —— *k 组等大的牌,k &gt; 1 时点数连续*(见 <see cref="WakengCombo"/>)。斗地主要算炸弹、
/// 四带二、飞机带翅膀,而且炸弹跨型压,候选空间大一个量级 —— 那是另一个变更的事。
/// </para>
/// </summary>
public static class WakengFollows
{
    /// <summary>连牌最少几组 —— 与 <see cref="WakengCombo"/> 的「3 组起」同一条。</summary>
    private const int MinRunGroups = 3;

    /// <summary>
    /// 这手牌在当前局面下全部合法的出法,**按先弱后强**排。
    /// </summary>
    /// <param name="hand">这个座位手上的牌。</param>
    /// <param name="onTable">
    /// 桌上等着被压的那一手;<c>null</c> 表示自由首出(开局第一手,或连续两家过牌之后)。
    /// </param>
    public static IReadOnlyList<IReadOnlyList<Card>> For(
        IReadOnlyList<Card> hand,
        WakengCombo? onTable)
    {
        var byRank = hand
            .GroupBy(c => c.Rank)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c).ToList());

        var found = new List<(WakengCombo Combo, IReadOnlyList<Card> Cards)>();

        // 单组:单 / 对 / 三头 / 四头。组的大小取到手上真有的张数。
        foreach (var (rank, cards) in byRank)
        {
            for (var size = 1; size <= cards.Count; size++)
            {
                Add(found, cards.Take(size).ToList());
            }
        }

        // 连牌:顺子 / 连对 / 飞机 / 火箭。3 组起,点数连续,且每个点数都能进连牌。
        var runnable = WakengRank.RunnableRanks
            .Where(r => byRank.ContainsKey(r))
            .OrderBy(WakengRank.RunIndex)
            .ToList();
        for (var size = 1; size <= 4; size++)
        {
            // 只保留「这个点数至少有 size 张」的那些,再在其中找连续段。
            var usable = runnable.Where(r => byRank[r].Count >= size).ToList();
            foreach (var (start, length) in ConsecutiveRuns(usable))
            {
                for (var groups = MinRunGroups; groups <= length; groups++)
                {
                    for (var offset = 0; offset + groups <= length; offset++)
                    {
                        var picked = usable
                            .Skip(start + offset)
                            .Take(groups)
                            .SelectMany(r => byRank[r].Take(size))
                            .ToList();
                        Add(found, picked);
                    }
                }
            }
        }

        return found
            .Where(f => onTable is null || f.Combo.Beats(onTable.Value))
            .OrderBy(f => f.Combo.CardCount)
            .ThenBy(f => f.Combo.TopStrength)
            .Select(f => f.Cards)
            .ToList();
    }

    /// <summary>
    /// 这手牌**出得起吗** —— 亦即 <see cref="For"/> 非空。
    /// <para>
    /// 单独给一个名字,是因为它是 `seatView` 上那个布尔的唯一来源:
    /// 一条断言把两者钉在一起(`canFollow == For(...).Count &gt; 0`),
    /// **两个出口读同一个事实,那就该有一条断言把它们钉住**。
    /// </para>
    /// </summary>
    /// <param name="hand">这个座位手上的牌。</param>
    /// <param name="onTable">桌上那一手;<c>null</c> 表示自由首出。</param>
    public static bool CanFollow(IReadOnlyList<Card> hand, WakengCombo? onTable)
        => For(hand, onTable).Count > 0;

    /// <summary>认出来就收下 —— 认不出来的组合根本不是候选。</summary>
    private static void Add(
        List<(WakengCombo, IReadOnlyList<Card>)> found,
        List<Card> cards)
    {
        if (WakengCombo.TryRecognise(cards, out var combo))
        {
            found.Add((combo, cards));
        }
    }

    /// <summary>
    /// 在一串已按连牌位置升序排好的点数里,找出全部**连续段**,给出 (起点下标, 长度)。
    /// </summary>
    private static IEnumerable<(int Start, int Length)> ConsecutiveRuns(IReadOnlyList<CardRank> ranks)
    {
        var start = 0;
        for (var i = 1; i <= ranks.Count; i++)
        {
            var broken = i == ranks.Count
                || WakengRank.RunIndex(ranks[i]) != WakengRank.RunIndex(ranks[i - 1]) + 1;
            if (!broken)
            {
                continue;
            }
            if (i - start >= MinRunGroups)
            {
                yield return (start, i - start);
            }
            start = i;
        }
    }
}
