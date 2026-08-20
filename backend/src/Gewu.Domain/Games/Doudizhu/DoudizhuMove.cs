using System;
using System.Collections.Generic;
using System.Linq;
using Gewu.Domain.Exceptions;

using Gewu.Domain.Games.Cards;

namespace Gewu.Domain.Games.Doudizhu;

/// <summary>一步斗地主是哪一种。</summary>
public enum DoudizhuMoveKind
{
    /// <summary>叫分(含不叫)。</summary>
    Bid = 0,

    /// <summary>出牌阶段过牌。</summary>
    Pass = 1,

    /// <summary>出牌。</summary>
    Play = 2,
}

/// <summary>
/// 一步斗地主,以及它在 <c>Move.Text</c> 里的编码。
/// <para>
/// 三种形式:<c>bid:0</c>…<c>bid:3</c>、<c>pass</c>、<c>play:&lt;cards&gt;</c>。
/// </para>
/// <para>
/// <b>标签不是装饰。</b> 牌的字母表是 <c>A-Za-z@#</c>,所以**一个由字母组成的英文词就是一手合法的
/// 牌**(<c>cab</c> = 三张)。没有标签,"这是动作还是牌"就没法判。标签在第一个 <c>:</c> 之前
/// (或整串恰为 <c>pass</c>)让解析无歧义。
/// </para>
/// <para>
/// <b>这条理由的第一版是错的,而错在哪里值得记下来。</b> 我原本写的是「<c>"pass"</c> 就是一手
/// 四张牌」—— 而 <c>"pass"</c> 里有两个 <c>s</c>,<c>Card.DecodeMany</c> 恰好拒绝重复的牌,
/// 所以那一串会抛。也就是说 <c>"pass"</c> 的安全**是运气**(那个词刚好有重复字母),不是标签的
/// 功劳。换成 <c>paw</c> / <c>cab</c> 这样没有重复字母的词,歧义就真的在那里。
/// **一个正确的结论配一个错的理由,下一个人照那个理由推下去就会走错。**
/// </para>
/// <para>
/// 标签是可读的英文而不是单字符前缀:<c>Move.Text</c> 会被人在数据库里直接读,而 <c>"cABC"</c>
/// 与 <c>"play:ABC"</c> 差 5 个字符、差一整个"这是什么"。
/// </para>
/// <para>
/// 长度装得进 <c>Move.Text</c> 的 64 字符:<c>play:</c> 五个字符加最多 20 张牌(地主的全部手牌)
/// = 25。
/// </para>
/// </summary>
public readonly record struct DoudizhuMove
{
    private const string PassText = "pass";
    private const string BidPrefix = "bid:";
    private const string PlayPrefix = "play:";

    /// <summary>不叫。</summary>
    public const int NoBid = 0;

    private DoudizhuMove(DoudizhuMoveKind kind, int bid, IReadOnlyList<Card> cards)
    {
        Kind = kind;
        Bid = bid;
        Cards = cards;
    }

    /// <summary>这一步是哪一种。</summary>
    public DoudizhuMoveKind Kind { get; }

    /// <summary>叫的分;非叫分时为 <c>0</c>。</summary>
    public int Bid { get; }

    /// <summary>出的牌;非出牌时为空。</summary>
    public IReadOnlyList<Card> Cards { get; }

    /// <summary>叫分(<c>0</c> 是不叫)。</summary>
    /// <param name="points">叫的分,0 到 3。</param>
    /// <exception cref="InvalidMoveException">分数越界。</exception>
    public static DoudizhuMove Bidding(int points)
    {
        if (points is < NoBid or > DoudizhuScoring.MaxBaseScore)
        {
            throw new InvalidMoveException(
                $"A bid is {NoBid}–{DoudizhuScoring.MaxBaseScore}; got {points}.");
        }
        return new DoudizhuMove(DoudizhuMoveKind.Bid, points, []);
    }

    /// <summary>过牌。</summary>
    public static DoudizhuMove Passing() => new(DoudizhuMoveKind.Pass, NoBid, []);

    /// <summary>出牌。</summary>
    /// <param name="cards">出的牌,非空。</param>
    /// <exception cref="InvalidMoveException">一张牌都没出。</exception>
    public static DoudizhuMove Playing(IReadOnlyList<Card> cards)
    {
        if (cards.Count == 0)
        {
            throw new InvalidMoveException("A play must contain at least one card.");
        }
        return new DoudizhuMove(DoudizhuMoveKind.Play, NoBid, cards);
    }

    /// <summary>编码成 <c>Move.Text</c> 的内容。</summary>
    public string Encode() => Kind switch
    {
        DoudizhuMoveKind.Bid => $"{BidPrefix}{Bid}",
        DoudizhuMoveKind.Pass => PassText,
        DoudizhuMoveKind.Play => PlayPrefix + Card.Encode(Cards),
        _ => throw new InvalidOperationException($"Unknown move kind {Kind}."),
    };

    /// <summary>解析 <c>Move.Text</c> 的内容。</summary>
    /// <param name="text">一步棋的文本。</param>
    /// <exception cref="InvalidMoveException">认不出来。</exception>
    public static DoudizhuMove Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new InvalidMoveException("A doudizhu move cannot be empty.");
        }

        if (text == PassText)
        {
            return Passing();
        }

        if (text.StartsWith(BidPrefix, StringComparison.Ordinal))
        {
            var rest = text[BidPrefix.Length..];
            // int.TryParse 会接受 "+2" / " 2" / "2 " 之类 —— 而 Move.Text 是持久化格式,
            // 同一步棋只该有一种写法。所以这里要求恰好一位数字。
            if (rest.Length != 1 || rest[0] is < '0' or > '9')
            {
                throw new InvalidMoveException($"'{text}' is not a bid.");
            }
            return Bidding(rest[0] - '0');
        }

        if (text.StartsWith(PlayPrefix, StringComparison.Ordinal))
        {
            // 解牌与"畸形的牌是一次领域拒绝"这条映射在 `CardPlay` 里,与挖坑共用 —— 见那个类的
            // 说明。此前这里是一段 `try/catch`,而那个 `catch` 是一条真缺陷的修复
            // (`play:!!!` 会以 `FormatException` 冒出去变成 500);挖坑要写第二个解析器,
            // 而**一个需要被记得的 catch 会在第三个解析器那里被忘掉**。
            //
            // **这里此前还有一条"同一张牌不能出现两次"的检查,而它是死代码** ——
            // `Card.DecodeMany` 早就在拦这件事。与 add-doudizhu-cards 里 `WingsAreLegal`
            // 是同一个缺陷,发现方式也一样:为它写的那条测试拿到的是 `FormatException`。
            return Playing(CardPlay.Decode(text[PlayPrefix.Length..], text));
        }

        throw new InvalidMoveException($"'{text}' is not a doudizhu move.");
    }
}
