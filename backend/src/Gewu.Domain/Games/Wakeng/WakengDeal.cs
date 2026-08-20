using System;
using System.Collections.Generic;
using System.Linq;
using Gewu.Domain.Games.Cards;

namespace Gewu.Domain.Games.Wakeng;

/// <summary>一次挖坑发牌:三家的手牌 + 四张底牌。</summary>
/// <param name="Hands">按座位号:0、1、2 各 16 张。</param>
/// <param name="Kitty">底牌 4 张。</param>
public readonly record struct WakengDeal(
    IReadOnlyList<IReadOnlyList<Card>> Hands,
    IReadOnlyList<Card> Kitty)
{
    /// <summary>三个座位。</summary>
    public const int SeatCount = 3;

    /// <summary>每家 16 张。</summary>
    public const int HandSize = 16;

    /// <summary>底牌 4 张 —— 比斗地主多一张。</summary>
    public const int KittySize = 4;

    /// <summary>一副 52 张,**不含大小王**。</summary>
    public const int DeckSize = 52;

    /// <summary>
    /// 从一个种子发牌。**同一个种子永远发出同一副牌** —— 重放一局靠的就是这一点。
    /// <para>
    /// 洗法在 <see cref="CardShuffle"/> 里(与斗地主共用):算法写死、不用运行时 RNG,
    /// 因为这副牌必须在任何运行时上都发得一模一样。
    /// </para>
    /// </summary>
    /// <param name="seed">发牌种子。</param>
    public static WakengDeal FromSeed(int seed)
    {
        var deck = Card.SuitedDeck.ToList();
        CardShuffle.Shuffle(deck, seed);

        var hands = new List<IReadOnlyList<Card>>(SeatCount);
        for (var seat = 0; seat < SeatCount; seat++)
        {
            hands.Add(deck.Skip(seat * HandSize).Take(HandSize).OrderBy(c => c).ToList());
        }
        var kitty = deck.Skip(SeatCount * HandSize).Take(KittySize).OrderBy(c => c).ToList();

        return new WakengDeal(hands, kitty);
    }

    /// <summary>
    /// 首叫权:**拿底牌前持有最小 ♣ 的座位**,以及那张牌。
    /// <para>
    /// 规则原文:「持有 ♣4(拿底牌前最小的 ♣ 牌)的玩家获得首叫权和首出权……
    /// 若没人有 ♣4,则拿 ♣5 者首叫,依此类推。」所以这里按挖坑的大小从小到大扫梅花,
    /// 第一张落在某家手里的就是它。
    /// </para>
    /// <para>
    /// **它一定找得到。** 十三张梅花,底牌只有四张 —— 至少九张在手上。找不到只能是这份发牌
    /// 本身坏了(比如解码出了一副不完整的牌),所以那种情况抛,而不是默默返回 0 号座位:
    /// 一个默默的默认会让「首叫权算错」表现成「0 号总是先叫」,而那要打很多局才看出来。
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">三家手上一张梅花也没有。</exception>
    public (int Seat, Card Card) FirstBidder()
    {
        foreach (var rank in WakengRank.RunnableRanks
            .Concat([CardRank.Ace, CardRank.Two, CardRank.Three])
            .OrderBy(WakengRank.Strength))
        {
            var card = new Card(rank, CardSuit.Clubs);
            for (var seat = 0; seat < Hands.Count; seat++)
            {
                if (Hands[seat].Contains(card))
                {
                    return (seat, card);
                }
            }
        }

        throw new InvalidOperationException(
            "No club in any hand; a 52-card deal with a 4-card kitty always leaves nine.");
    }

    /// <summary>
    /// 把整副发牌编码成一个字符串,供服务端保存。
    /// <para>
    /// **这个字符串 MUST NOT 发给客户端** —— 它就是三家的底牌。与斗地主同一条:
    /// *客户端算不出来的东西,客户端就骗不了*。
    /// </para>
    /// </summary>
    public string Encode() =>
        string.Join("/", Hands.Select(Card.Encode).Append(Card.Encode(Kitty)));

    /// <summary>解回一次发牌。</summary>
    /// <param name="encoded"><see cref="Encode"/> 的输出。</param>
    /// <exception cref="FormatException">段数不对、张数不对、有重复的牌,或含王。</exception>
    public static WakengDeal Decode(string encoded)
    {
        var parts = encoded.Split('/');
        if (parts.Length != SeatCount + 1)
        {
            throw new FormatException(
                $"A deal has {SeatCount} hands and a kitty; got {parts.Length} sections.");
        }

        var hands = new List<IReadOnlyList<Card>>(SeatCount);
        for (var seat = 0; seat < SeatCount; seat++)
        {
            var hand = Card.DecodeMany(parts[seat]);
            if (hand.Count != HandSize)
            {
                throw new FormatException(
                    $"Seat {seat} has {hand.Count} cards; expected {HandSize}.");
            }
            hands.Add(hand);
        }

        var kitty = Card.DecodeMany(parts[SeatCount]);
        if (kitty.Count != KittySize)
        {
            throw new FormatException($"The kitty has {kitty.Count} cards; expected {KittySize}.");
        }

        var all = hands.SelectMany(h => h).Concat(kitty).ToList();
        if (all.Distinct().Count() != DeckSize)
        {
            throw new FormatException(
                $"A deal must use all {DeckSize} distinct cards; got {all.Distinct().Count()}.");
        }

        // 挖坑去掉大小王 —— 一副带王的牌能通过上面每一条,而它会让「3 最大」这条规则失去意义。
        if (all.Any(c => c.IsJoker))
        {
            throw new FormatException("挖坑 has no jokers; this deal contains one.");
        }

        return new WakengDeal(hands, kitty);
    }
}
