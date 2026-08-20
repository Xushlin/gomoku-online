using System;
using System.Collections.Generic;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Cards;

namespace Gewu.Domain.Games.Wakeng;

/// <summary>一步挖坑是哪一种。</summary>
public enum WakengMoveKind
{
    /// <summary>叫分(含不挖)。</summary>
    Bid = 0,

    /// <summary>出牌阶段过牌。</summary>
    Pass = 1,

    /// <summary>出牌。</summary>
    Play = 2,
}

/// <summary>
/// 一步挖坑,以及它在 <c>Move.Text</c> 里的编码:<c>bid:0</c>…<c>bid:3</c>、<c>pass</c>、
/// <c>play:&lt;cards&gt;</c>。
/// <para>
/// <b>语法与斗地主一模一样,而这是**另一个类型**。</b> 理由与 <c>hoist-card-model</c> 拒绝把
/// <c>TetrisPieceSequence</c> 并进 <c>CardShuffle</c> 时用的是同一条判据:
/// **按"是不是同一件事"分,不按"代码长得像不像"分**。
/// </para>
/// <para>
/// <c>Card</c> 必须提出去,是因为挖坑**真的在用同一批值** —— 同样 52 张、同样的编码字母表、
/// 同一个 <c>DecodeMany</c>,那是一个事实。而 <see cref="WakengMove"/> 与 <c>DoudizhuMove</c>
/// 产出**不同的字符串**、喂给**不同的规则**,没有任何一段代码同时读两者:共享的只有形状。
/// **形状相同不等于事实相同**,而"它们可以分歧"(挖坑哪天要 <c>bid:4</c>,斗地主一行不动)
/// 正是"这不是一个事实"的检验。
/// </para>
/// <para>
/// 唯一真正必要的那一小块 —— 「畸形的牌是一次领域拒绝而不是 <c>FormatException</c>」——
/// 在 <see cref="CardPlay"/> 里共享,因为**它的重复会重造一个量过的缺陷**(<c>play:!!!</c>
/// 变成 500)。触发条件:第三个牌类棋种落地时,这笔账重算。
/// </para>
/// <para>
/// <b>标签不是装饰。</b> 牌的字母表是 <c>A-Za-z@#</c>,所以一个由字母组成的英文词就是一手
/// 合法的牌(<c>cab</c> = 三张)。<c>"pass"</c> 恰好有两个 <c>s</c> 而重复的牌会被拒,所以那一串
/// 本身是安全的 —— 但那是**运气**,不是标签的功劳,而 <c>paw</c> 这种词的歧义是真的在那里。
/// </para>
/// <para>
/// 长度装得进 <c>Move.Text</c> 的 64 字符:<c>play:</c> 五个字符加最多 20 张牌
/// (挖坑者拿完底牌的全部手牌)= 25。
/// </para>
/// </summary>
public readonly record struct WakengMove
{
    private const string PassText = "pass";
    private const string BidPrefix = "bid:";
    private const string PlayPrefix = "play:";

    /// <summary>不挖。</summary>
    public const int NoBid = 0;

    private WakengMove(WakengMoveKind kind, int bid, IReadOnlyList<Card> cards)
    {
        Kind = kind;
        Bid = bid;
        Cards = cards;
    }

    /// <summary>这一步是哪一种。</summary>
    public WakengMoveKind Kind { get; }

    /// <summary>叫的分;非叫分时为 <c>0</c>。</summary>
    public int Bid { get; }

    /// <summary>出的牌;非出牌时为空。</summary>
    public IReadOnlyList<Card> Cards { get; }

    /// <summary>叫分(<c>0</c> 是不挖)。</summary>
    /// <param name="points">叫的分,0 到 3。</param>
    /// <exception cref="InvalidMoveException">分数越界。</exception>
    public static WakengMove Bidding(int points)
    {
        if (points is < NoBid or > WakengScoring.MaxBid)
        {
            throw new InvalidMoveException(
                $"A bid is {NoBid}–{WakengScoring.MaxBid}; got {points}.");
        }
        return new WakengMove(WakengMoveKind.Bid, points, []);
    }

    /// <summary>过牌。</summary>
    public static WakengMove Passing() => new(WakengMoveKind.Pass, NoBid, []);

    /// <summary>出牌。</summary>
    /// <param name="cards">出的牌,非空。</param>
    /// <exception cref="InvalidMoveException">一张牌都没出。</exception>
    public static WakengMove Playing(IReadOnlyList<Card> cards)
    {
        if (cards.Count == 0)
        {
            throw new InvalidMoveException("A play must contain at least one card.");
        }
        return new WakengMove(WakengMoveKind.Play, NoBid, cards);
    }

    /// <summary>编码成 <c>Move.Text</c> 的内容。</summary>
    public string Encode() => Kind switch
    {
        WakengMoveKind.Bid => $"{BidPrefix}{Bid}",
        WakengMoveKind.Pass => PassText,
        WakengMoveKind.Play => PlayPrefix + Card.Encode(Cards),
        _ => throw new InvalidOperationException($"Unknown move kind {Kind}."),
    };

    /// <summary>解析 <c>Move.Text</c> 的内容。</summary>
    /// <param name="text">一步棋的文本。</param>
    /// <exception cref="InvalidMoveException">认不出来。</exception>
    public static WakengMove Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new InvalidMoveException("A wakeng move cannot be empty.");
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
            return Playing(CardPlay.Decode(text[PlayPrefix.Length..], text));
        }

        throw new InvalidMoveException($"'{text}' is not a wakeng move.");
    }
}
