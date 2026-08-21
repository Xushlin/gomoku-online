using System.Collections.Generic;
using System.Linq;
using Gewu.Domain.Games.Cards;

namespace Gewu.Domain.Games.Doudizhu;

/// <summary>
/// 一手牌在当前局面下**全部**合法的出法 —— 「要不起」与「提示」共用的那一个事实。
/// <para>
/// <b>它与 <c>WakengFollows</c> 是两份实现,而那不是重复。</b> 两处差别都是结构性的:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>跨型压。</b> 挖坑没有炸弹,`Beats` 要求同型同长度,所以候选只在一种牌型之内;
///     斗地主的炸弹压任何非炸弹、王炸压一切,所以候选是**两部分的并集**。
///   </description></item>
///   <item><description>
///     <b>带牌。</b> 挖坑的三头四头都不能带;斗地主有六种带**填充牌**的牌型,而那带来一个
///     组合维度 —— 见 <see cref="For"/> 上那段关于「只列一条」的说明。
///   </description></item>
/// </list>
/// <para>
/// 共享的是接缝(<c>IPlayHintRules</c>),不是枚举。**形状相同不等于事实相同**,而
/// 「它们可以分歧」正是这条的检验。
/// </para>
/// </summary>
public static class DoudizhuFollows
{
    /// <summary>
    /// 这手牌在当前局面下全部合法的出法,按**先弱后强**排。
    /// <para>
    /// <b>带填充牌的六种牌型只列一条,填充牌取最弱。</b> `Beats` 只看决定大小的那一部分
    /// (三张 / 四张 / 飞机的最大一组),所以同一个部分有很多写法 —— 它们**只有填充牌不同**。
    /// 一手 20 张牌里的一个三条能配十几个单张,全列出来会让提示按钮变成「在同一个三条的
    /// 十几种写法里轮换」,那不是提示。
    /// </para>
    /// <para>
    /// 那是一处**判断**:填充牌是要扔掉的东西,扔最弱的是默认最优。代价是一个想用
    /// `333 + 一张 K` 骗对家的玩家,提示不会给他那一手 —— 他仍然可以手工点。
    /// **提示是建议,不是代打。**
    /// </para>
    /// </summary>
    /// <param name="hand">这个座位手上的牌。</param>
    /// <param name="onTable">桌上等着被压的那一手;<c>null</c> 表示自由首出。</param>
    public static IReadOnlyList<IReadOnlyList<Card>> For(
        IReadOnlyList<Card> hand,
        CardCombo? onTable)
    {
        var found = new List<(CardCombo Combo, IReadOnlyList<Card> Cards)>();
        var byRank = hand
            .Where(c => !c.IsJoker)
            .GroupBy(c => c.Rank)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x).ToList());
        var jokers = hand.Where(c => c.IsJoker).OrderBy(c => c).ToList();

        AddPlainGroups(found, byRank);
        AddRuns(found, byRank);
        AddWithKickers(found, byRank, hand);
        AddBombs(found, byRank, jokers);

        return found
            .Where(f => onTable is null || f.Combo.Beats(onTable.Value))
            .OrderBy(f => Rankings.Tier(f.Combo))
            .ThenBy(f => f.Cards.Count)
            .ThenBy(f => (int)f.Combo.Key)
            .Select(f => f.Cards)
            .ToList();
    }

    /// <summary>出得起吗 —— 亦即 <see cref="For"/> 非空。</summary>
    /// <param name="hand">这个座位手上的牌。</param>
    /// <param name="onTable">桌上那一手;<c>null</c> 表示自由首出。</param>
    public static bool CanFollow(IReadOnlyList<Card> hand, CardCombo? onTable)
        => For(hand, onTable).Count > 0;

    /// <summary>单 / 对 / 三张。四张同点数走 <see cref="AddBombs"/>。</summary>
    private static void AddPlainGroups(
        List<(CardCombo, IReadOnlyList<Card>)> found,
        Dictionary<CardRank, List<Card>> byRank)
    {
        foreach (var (_, cards) in byRank)
        {
            for (var size = 1; size <= System.Math.Min(3, cards.Count); size++)
            {
                Add(found, cards.Take(size).ToList());
            }
        }
    }

    /// <summary>单顺(≥5)/ 双顺(≥3 组)/ 飞机不带(≥2 组)。</summary>
    private static void AddRuns(
        List<(CardCombo, IReadOnlyList<Card>)> found,
        Dictionary<CardRank, List<Card>> byRank)
    {
        // 2 与王进不了顺子 —— `CardCombo` 自己的上界是 A。
        var runnable = byRank.Keys
            .Where(r => r <= CardRank.Ace)
            .OrderBy(r => (int)r)
            .ToList();

        for (var size = 1; size <= 3; size++)
        {
            var usable = runnable.Where(r => byRank[r].Count >= size).ToList();
            var minGroups = size switch { 1 => 5, 2 => 3, _ => 2 };
            foreach (var (start, length) in ConsecutiveRuns(usable))
            {
                for (var groups = minGroups; groups <= length; groups++)
                {
                    for (var offset = 0; offset + groups <= length; offset++)
                    {
                        Add(found, usable
                            .Skip(start + offset)
                            .Take(groups)
                            .SelectMany(r => byRank[r].Take(size))
                            .ToList());
                    }
                }
            }
        }
    }

    /// <summary>
    /// 带填充牌的六种 —— **每个「决定大小的部分」只列一条**,填充牌取最弱。
    /// </summary>
    private static void AddWithKickers(
        List<(CardCombo, IReadOnlyList<Card>)> found,
        Dictionary<CardRank, List<Card>> byRank,
        IReadOnlyList<Card> hand)
    {
        var triples = byRank.Where(kv => kv.Value.Count >= 3).Select(kv => kv.Key).ToList();
        var quads = byRank.Where(kv => kv.Value.Count == 4).Select(kv => kv.Key).ToList();

        // 三带一 / 三带一对
        foreach (var rank in triples)
        {
            var body = byRank[rank].Take(3).ToList();
            AddWithFiller(found, hand, body, singles: 1, pairs: 0);
            AddWithFiller(found, hand, body, singles: 0, pairs: 1);
        }

        // 四带两单 / 四带两对
        foreach (var rank in quads)
        {
            var body = byRank[rank].ToList();
            AddWithFiller(found, hand, body, singles: 2, pairs: 0);
            AddWithFiller(found, hand, body, singles: 0, pairs: 2);
        }

        // 飞机带翅膀 —— 连续的三张组,每组配一张单或一对。
        var runnableTriples = triples.Where(r => r <= CardRank.Ace).OrderBy(r => (int)r).ToList();
        foreach (var (start, length) in ConsecutiveRuns(runnableTriples))
        {
            for (var groups = 2; groups <= length; groups++)
            {
                for (var offset = 0; offset + groups <= length; offset++)
                {
                    var body = runnableTriples
                        .Skip(start + offset)
                        .Take(groups)
                        .SelectMany(r => byRank[r].Take(3))
                        .ToList();
                    AddWithFiller(found, hand, body, singles: groups, pairs: 0);
                    AddWithFiller(found, hand, body, singles: 0, pairs: groups);
                }
            }
        }
    }

    /// <summary>
    /// 给 <paramref name="body"/> 配上最弱的填充牌。配不齐就不产生候选。
    /// </summary>
    private static void AddWithFiller(
        List<(CardCombo, IReadOnlyList<Card>)> found,
        IReadOnlyList<Card> hand,
        IReadOnlyList<Card> body,
        int singles,
        int pairs)
    {
        // **填充牌 MUST 是别的点数,而这是一条真缺陷的修复。**
        //
        // 第一版只排除了 body 里那几张**具体的牌**,于是一个三条的填充牌取到了同点数的
        // 第四张 —— `777 + 7` 被 `Recognise` 认成**炸弹**,于是炸弹进了候选两次
        // (一次从这里、一次从 `AddBombs`)。一个三带一的填充牌本来就该是另一个点数:
        // 同点数的第四张凑出来的是炸弹,不是三带一。
        var bodyRanks = body.Select(c => c.Rank).ToHashSet();
        var spare = hand.Where(c => !bodyRanks.Contains(c.Rank)).ToList();
        var filler = new List<Card>();

        if (pairs > 0)
        {
            // 对翅膀:从最弱的点数开始找**还剩两张**的。王不能当翅膀(两张王是王炸)。
            var available = spare
                .Where(c => !c.IsJoker)
                .GroupBy(c => c.Rank)
                .Where(g => g.Count() >= 2)
                .OrderBy(g => (int)g.Key)
                .Take(pairs)
                .ToList();
            if (available.Count < pairs) return;
            filler.AddRange(available.SelectMany(g => g.OrderBy(c => c).Take(2)));
        }

        if (singles > 0)
        {
            var available = spare
                .Where(c => !filler.Contains(c))
                .OrderBy(c => (int)c.Rank)
                .Take(singles)
                .ToList();
            if (available.Count < singles) return;
            filler.AddRange(available);
        }

        Add(found, [.. body, .. filler]);
    }

    /// <summary>炸弹与王炸。</summary>
    private static void AddBombs(
        List<(CardCombo, IReadOnlyList<Card>)> found,
        Dictionary<CardRank, List<Card>> byRank,
        List<Card> jokers)
    {
        foreach (var (_, cards) in byRank.Where(kv => kv.Value.Count == 4))
        {
            Add(found, cards.ToList());
        }
        if (jokers.Count == 2)
        {
            Add(found, jokers.ToList());
        }
    }

    /// <summary>认出来就收下 —— 认不出来的组合根本不是候选。</summary>
    private static void Add(
        List<(CardCombo, IReadOnlyList<Card>)> found,
        List<Card> cards)
    {
        if (CardCombo.Recognise(cards) is CardCombo combo)
        {
            found.Add((combo, cards));
        }
    }

    /// <summary>在一串已按点数升序排好的点数里,找出全部连续段。</summary>
    private static IEnumerable<(int Start, int Length)> ConsecutiveRuns(IReadOnlyList<CardRank> ranks)
    {
        var start = 0;
        for (var i = 1; i <= ranks.Count; i++)
        {
            var broken = i == ranks.Count || (int)ranks[i] != (int)ranks[i - 1] + 1;
            if (!broken)
            {
                continue;
            }
            if (i - start >= 2)
            {
                yield return (start, i - start);
            }
            start = i;
        }
    }

    /// <summary>
    /// 排序用的分层 —— **炸弹排在最后**。
    /// <para>
    /// 提示从最弱的一手开始,而「先弱后强」在跨型的候选里不是一个数能表达的:
    /// 一个炸弹比任何非炸弹都强,而它的张数可能更少。所以先按层,层内再按张数与点数。
    /// **不把炸弹混进普通牌型的排序里**,是因为那会让提示的第一手就是炸弹。
    /// </para>
    /// </summary>
    private static class Rankings
    {
        internal static int Tier(CardCombo combo) => combo.Kind switch
        {
            ComboKind.Rocket => 2,
            ComboKind.Bomb => 1,
            _ => 0,
        };
    }
}
