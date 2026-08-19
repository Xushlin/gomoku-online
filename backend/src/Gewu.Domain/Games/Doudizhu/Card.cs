using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gewu.Domain.Games.Doudizhu;

/// <summary>
/// 一张牌的点数。**数值就是大小顺序**,所以比大小是整数比较,不需要查表。
/// <para>
/// 3 最小,然后一路到 A,再是 2,最后两张王。这个顺序是斗地主的,不是扑克的 ——
/// 2 比 A 大,而 A 不能当 1 用(顺子里 A 是上界)。
/// </para>
/// </summary>
public enum CardRank
{
    /// <summary>3 —— 最小。</summary>
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14,

    /// <summary>2 —— 比 A 大。</summary>
    Two = 15,

    /// <summary>小王。</summary>
    SmallJoker = 16,

    /// <summary>大王 —— 最大的单张。</summary>
    BigJoker = 17,
}

/// <summary>
/// 花色。**不参与任何比较** —— 两张同点数的牌在斗地主里完全等价,花色只影响显示。
/// 王没有花色。
/// </summary>
public enum CardSuit
{
    /// <summary>王专用 —— 没有花色。</summary>
    None = 0,
    Clubs = 1,
    Diamonds = 2,
    Hearts = 3,
    Spades = 4,
}

/// <summary>
/// 一张牌。
/// <para>
/// **它有一个一字符编码,而那个编码是持久化格式的一部分,永远不能变。** 一手牌最多 20 张,
/// 而 <c>Move.Text</c> 的上限是 64 字符 —— 所以斗地主的出牌**用现有的文本载荷就装得下**,
/// 不需要第四种载荷、也不需要为它加列。
/// </para>
/// <para>
/// `generalize-match-payload` 当年拒绝 JSON 列时留的触发条件是「真出现不规则走子时再加列」。
/// 这条**不成立**:一手牌是常规的文本内容,不是新的维度 —— 与成语接龙的一个成语同类。
/// </para>
/// </summary>
public readonly record struct Card(CardRank Rank, CardSuit Suit) : IComparable<Card>
{
    /// <summary>一副牌 54 张。</summary>
    public const int DeckSize = 54;

    /// <summary>
    /// 编码用的字母表:52 张普通牌用 <c>A–Z</c> + <c>a–z</c>,两张王用 <c>@</c> 与 <c>#</c>。
    /// <para>
    /// **这是持久化格式,MUST NOT 改动** —— 改一个字符,所有历史对局的重放都会读出别的牌。
    /// 刻意避开引号、逗号、反斜杠:这些字符会在日志、CSV、JSON 里各被转义一次。
    /// </para>
    /// </summary>
    private const string Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz@#";

    /// <summary>普通牌的点数,3 到 2 共 13 种(不含王)。</summary>
    public static readonly IReadOnlyList<CardRank> SuitedRanks =
        Enumerable.Range((int)CardRank.Three, (int)CardRank.Two - (int)CardRank.Three + 1)
            .Select(x => (CardRank)x)
            .ToList();

    /// <summary>四种花色,固定顺序 —— 只影响编码与显示。</summary>
    public static readonly IReadOnlyList<CardSuit> Suits =
        [CardSuit.Clubs, CardSuit.Diamonds, CardSuit.Hearts, CardSuit.Spades];

    /// <summary>小王。</summary>
    public static readonly Card SmallJoker = new(CardRank.SmallJoker, CardSuit.None);

    /// <summary>大王。</summary>
    public static readonly Card BigJoker = new(CardRank.BigJoker, CardSuit.None);

    /// <summary>这张牌是不是王。</summary>
    public bool IsJoker => Rank is CardRank.SmallJoker or CardRank.BigJoker;

    /// <summary>一副完整的 54 张牌,顺序固定(未洗)。</summary>
    public static IReadOnlyList<Card> FullDeck { get; } = BuildDeck();

    private static List<Card> BuildDeck()
    {
        var cards = new List<Card>(DeckSize);
        foreach (var rank in SuitedRanks)
        {
            foreach (var suit in Suits)
            {
                cards.Add(new Card(rank, suit));
            }
        }
        cards.Add(SmallJoker);
        cards.Add(BigJoker);
        return cards;
    }

    /// <summary>这张牌在字母表里的下标,<c>0</c> 到 <c>53</c>。</summary>
    private int Index => Rank switch
    {
        CardRank.SmallJoker => 52,
        CardRank.BigJoker => 53,
        _ => ((int)Rank - (int)CardRank.Three) * 4 + (Suits.ToList().IndexOf(Suit)),
    };

    /// <summary>这张牌的一字符编码。</summary>
    public char Encode() => Alphabet[Index];

    /// <summary>把一手牌编成字符串。顺序**按牌本身排序**,所以同一手牌只有一种写法。</summary>
    /// <param name="cards">要编码的牌。</param>
    public static string Encode(IEnumerable<Card> cards)
    {
        var sb = new StringBuilder();
        foreach (var card in cards.OrderBy(x => x))
        {
            sb.Append(card.Encode());
        }
        return sb.ToString();
    }

    /// <summary>解一个字符。</summary>
    /// <param name="c">编码字符。</param>
    /// <exception cref="FormatException">不是字母表里的字符。</exception>
    public static Card Decode(char c)
    {
        var index = Alphabet.IndexOf(c);
        if (index < 0)
        {
            throw new FormatException($"'{c}' is not a card.");
        }
        return index switch
        {
            52 => SmallJoker,
            53 => BigJoker,
            _ => new Card((CardRank)(index / 4 + (int)CardRank.Three), Suits[index % 4]),
        };
    }

    /// <summary>解一手牌。</summary>
    /// <param name="encoded">编码后的字符串。</param>
    /// <exception cref="FormatException">含非牌字符,或同一张牌出现两次。</exception>
    public static IReadOnlyList<Card> DecodeMany(string encoded)
    {
        var cards = new List<Card>(encoded.Length);
        var seen = new HashSet<char>();
        foreach (var c in encoded)
        {
            if (!seen.Add(c))
            {
                // 同一张牌不可能在一手里出现两次 —— 一副牌里它只有一张。
                // 早在这里拦下,规则层就不必再想"这手牌是不是自己重复了"。
                throw new FormatException($"Card '{c}' appears twice in '{encoded}'.");
            }
            cards.Add(Decode(c));
        }
        return cards;
    }

    /// <summary>按点数排序;同点数按花色,只为让编码稳定。</summary>
    /// <param name="other">另一张牌。</param>
    public int CompareTo(Card other)
    {
        var byRank = ((int)Rank).CompareTo((int)other.Rank);
        return byRank != 0 ? byRank : ((int)Suit).CompareTo((int)other.Suit);
    }
}
