using System.Collections.Generic;
using System.Linq;

namespace Gewu.Domain.Games.Doudizhu;

/// <summary>斗地主的牌型。</summary>
public enum ComboKind
{
    /// <summary>单张。</summary>
    Single,

    /// <summary>对子。两张王不构成对子 —— 那是王炸。</summary>
    Pair,

    /// <summary>三张。</summary>
    Triplet,

    /// <summary>三带一。</summary>
    TripletWithSingle,

    /// <summary>三带一对。</summary>
    TripletWithPair,

    /// <summary>单顺(顺子),≥5 张连续单牌。</summary>
    Straight,

    /// <summary>双顺(连对),≥3 组连续对子。</summary>
    PairStraight,

    /// <summary>飞机(三顺),≥2 组连续三张,不带翅膀。</summary>
    Airplane,

    /// <summary>飞机带单翅膀。</summary>
    AirplaneWithSingles,

    /// <summary>飞机带对翅膀。</summary>
    AirplaneWithPairs,

    /// <summary>四带两单。**不是炸弹。**</summary>
    QuadWithSingles,

    /// <summary>四带两对。**不是炸弹。**</summary>
    QuadWithPairs,

    /// <summary>炸弹 —— 四张同点数。</summary>
    Bomb,

    /// <summary>王炸(火箭)—— 大王 + 小王。</summary>
    Rocket,
}

/// <summary>
/// 认出来的一个牌型:是什么、比大小看哪个点数、连了几组。
/// <para>
/// <see cref="Length"/> 是**连续组数**:单顺是牌数、双顺是对数、飞机是三张的组数,其余一律 1。
/// 顺子类只与同长度的比 —— 这是斗地主最容易实现错的一条。
/// </para>
/// </summary>
/// <param name="Kind">牌型。</param>
/// <param name="Key">比大小依据的点数。三带看三张、四带二看四张、顺子类看最大的那一组。</param>
/// <param name="Length">连续组数;非顺子类为 1。</param>
public readonly record struct CardCombo(ComboKind Kind, CardRank Key, int Length)
{
    /// <summary>顺子类的连续段上界 —— 2 和王都进不了顺子。</summary>
    private const CardRank RunCeiling = CardRank.Ace;

    /// <summary>单顺最少 5 张。</summary>
    private const int MinStraight = 5;

    /// <summary>双顺最少 3 组。</summary>
    private const int MinPairStraight = 3;

    /// <summary>飞机最少 2 组。</summary>
    private const int MinAirplane = 2;

    /// <summary>炸弹类 —— 压牌规则对它们有例外。</summary>
    public bool IsBombLike => Kind is ComboKind.Bomb or ComboKind.Rocket;

    /// <summary>
    /// 认牌型。认不出来返回 <c>null</c> —— 调用方据此拒绝这一手。
    /// </summary>
    /// <param name="cards">这一手打出的牌。</param>
    public static CardCombo? Recognise(IReadOnlyList<Card> cards)
    {
        if (cards.Count == 0)
        {
            return null;
        }

        var byRank = cards.GroupBy(c => c.Rank)
            .ToDictionary(g => g.Key, g => g.Count());
        var n = cards.Count;

        // 王炸先认:它是两张牌,而两张同点数是对子 —— 两张王不是对子。
        if (n == 2 && cards.All(c => c.IsJoker))
        {
            return new CardCombo(ComboKind.Rocket, CardRank.BigJoker, 1);
        }

        var quads = byRank.Where(kv => kv.Value == 4).Select(kv => kv.Key).ToList();
        var triples = byRank.Where(kv => kv.Value == 3).Select(kv => kv.Key).OrderBy(r => r).ToList();
        var pairs = byRank.Where(kv => kv.Value == 2).Select(kv => kv.Key).OrderBy(r => r).ToList();
        var singles = byRank.Where(kv => kv.Value == 1).Select(kv => kv.Key).OrderBy(r => r).ToList();

        if (n == 4 && quads.Count == 1)
        {
            return new CardCombo(ComboKind.Bomb, quads[0], 1);
        }

        if (byRank.Count == 1)
        {
            var rank = byRank.Keys.Single();
            return n switch
            {
                1 => new CardCombo(ComboKind.Single, rank, 1),
                2 => new CardCombo(ComboKind.Pair, rank, 1),
                3 => new CardCombo(ComboKind.Triplet, rank, 1),
                _ => null,
            };
        }

        // 三带一 / 三带一对。带的牌不受"不能拆炸弹"影响 —— 一手只有 4 / 5 张,
        // 拆不出四张来。
        if (triples.Count == 1)
        {
            if (n == 4 && singles.Count == 1)
            {
                return new CardCombo(ComboKind.TripletWithSingle, triples[0], 1);
            }
            if (n == 5 && pairs.Count == 1)
            {
                return new CardCombo(ComboKind.TripletWithPair, triples[0], 1);
            }
        }

        // 四带二。**它不是炸弹** —— 压不了别的牌型,也压不过任何炸弹。
        //
        // **这个分支末尾的 `return null` 是承重的。** 「翅膀 / 带牌 MUST NOT 取自一个四张同点数
        // 的组合」这条规则(家规 8.6 与 8.3-附 b)靠的就是它:含恰好一个四张的手牌一定先走到
        // 这里,要么认成 4+2 / 4+4,要么在这里被拒 —— 它**到不了**下面的飞机分支。
        //
        // 我原本在飞机那边另写了一个 `WingsAreLegal` 守卫来管这件事,而变异测试证明它是死代码:
        // 把它改坏,`888 999 TTT JJJ + 7777` 照样被拒,因为拒它的是这里。守卫已删,规则写在这。
        if (quads.Count == 1)
        {
            var rest = byRank.Where(kv => kv.Key != quads[0]).ToList();
            var restCount = rest.Sum(kv => kv.Value);
            if (n == 6 && restCount == 2 && rest.All(kv => kv.Value <= 2))
            {
                // 两张单牌**可以恰好同点数** —— 张数决定牌型,不看是否成对。
                return new CardCombo(ComboKind.QuadWithSingles, quads[0], 1);
            }
            if (n == 8 && restCount == 4 && rest.All(kv => kv.Value == 2))
            {
                return new CardCombo(ComboKind.QuadWithPairs, quads[0], 1);
            }
            return null;
        }

        // 单顺:全是单牌、连续、上界 A。
        if (n >= MinStraight && singles.Count == n && IsRun(singles))
        {
            return new CardCombo(ComboKind.Straight, singles[^1], n);
        }

        // 双顺:全是对子、≥3 组、连续。
        if (pairs.Count >= MinPairStraight && pairs.Count * 2 == n && IsRun(pairs))
        {
            return new CardCombo(ComboKind.PairStraight, pairs[^1], pairs.Count);
        }

        // 飞机:先认不带翅膀的(纯三顺),再认带翅膀的。
        if (triples.Count >= MinAirplane && IsRun(triples))
        {
            var m = triples.Count;
            if (n == m * 3)
            {
                return new CardCombo(ComboKind.Airplane, triples[^1], m);
            }
            if (n == m * 4)
            {
                return new CardCombo(ComboKind.AirplaneWithSingles, triples[^1], m);
            }
            if (n == m * 5 && WingsArePairs(byRank, triples, m))
            {
                return new CardCombo(ComboKind.AirplaneWithPairs, triples[^1], m);
            }
        }

        return null;
    }

    /// <summary>
    /// 这一手能不能压过 <paramref name="table"/>。
    /// </summary>
    /// <param name="table">桌上当前要压的那一手。</param>
    public bool Beats(CardCombo table)
    {
        // 王炸压一切。
        if (Kind == ComboKind.Rocket)
        {
            return table.Kind != ComboKind.Rocket;
        }
        if (table.Kind == ComboKind.Rocket)
        {
            return false;
        }

        // 炸弹压任何非炸弹,不论张数是否相同;炸弹之间按点数。
        if (Kind == ComboKind.Bomb)
        {
            return table.Kind != ComboKind.Bomb || Key > table.Key;
        }
        if (table.Kind == ComboKind.Bomb)
        {
            return false;
        }

        // 其余:同牌型、同长度、依据更大。
        return Kind == table.Kind && Length == table.Length && Key > table.Key;
    }

    /// <summary>这些点数是不是连续的一段,且不越过上界。</summary>
    private static bool IsRun(List<CardRank> ranks)
    {
        if (ranks.Count == 0 || ranks[^1] > RunCeiling)
        {
            return false;
        }
        for (var i = 1; i < ranks.Count; i++)
        {
            if ((int)ranks[i] != (int)ranks[i - 1] + 1)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>翅膀是不是恰好 m 个对子。</summary>
    private static bool WingsArePairs(Dictionary<CardRank, int> byRank, List<CardRank> run, int m)
    {
        var wings = byRank.Where(kv => !run.Contains(kv.Key)).ToList();
        return wings.Count == m && wings.All(kv => kv.Value == 2);
    }
}
