using System;
using System.Collections.Generic;
using System.Linq;

using Gewu.Domain.Games.Cards;

namespace Gewu.Domain.Games.Doudizhu;

/// <summary>一次发牌的结果:三家的手牌 + 三张底牌。</summary>
/// <param name="Hands">按座位号:0、1、2 各 17 张。</param>
/// <param name="Kitty">底牌 3 张。</param>
public readonly record struct DoudizhuDeal(
    IReadOnlyList<IReadOnlyList<Card>> Hands,
    IReadOnlyList<Card> Kitty)
{
    /// <summary>三个座位。</summary>
    public const int SeatCount = 3;

    /// <summary>每家 17 张。</summary>
    public const int HandSize = 17;

    /// <summary>底牌 3 张。</summary>
    public const int KittySize = 3;

    /// <summary>
    /// 从一个种子发牌。**同一个种子永远发出同一副牌** —— 重放一局靠的就是这一点。
    /// </summary>
    /// <param name="seed">发牌种子。</param>
    public static DoudizhuDeal FromSeed(int seed)
    {
        var deck = Card.FullDeck.ToList();

        // 洗法与零状态陷阱都在 CardShuffle 里 —— 挖坑要洗同一副牌,而那会是这段
        // Fisher–Yates 加 xorshift32 的第三份副本。`The_encoded_deal_is_pinned` 钉住了
        // 这次搬家一个字节都没改变输出。
        CardShuffle.Shuffle(deck, seed);

        var hands = new List<IReadOnlyList<Card>>(SeatCount);
        for (var seat = 0; seat < SeatCount; seat++)
        {
            hands.Add(deck.Skip(seat * HandSize).Take(HandSize).OrderBy(c => c).ToList());
        }
        var kitty = deck.Skip(SeatCount * HandSize).Take(KittySize).OrderBy(c => c).ToList();

        return new DoudizhuDeal(hands, kitty);
    }

    /// <summary>
    /// 把整副发牌编码成一个字符串,供服务端保存。
    /// <para>
    /// **这个字符串 MUST NOT 发给客户端** —— 它就是三家的底牌。它是服务端侧的对局设置,
    /// 与成语纵横「答案不出服务端」是同一条:*客户端算不出来的东西,客户端就骗不了*。
    /// </para>
    /// </summary>
    public string Encode() =>
        string.Join(
            "/",
            Hands.Select(Card.Encode).Append(Card.Encode(Kitty)));

    /// <summary>解回一次发牌。</summary>
    /// <param name="encoded"><see cref="Encode"/> 的输出。</param>
    /// <exception cref="FormatException">段数不对、张数不对,或有重复的牌。</exception>
    public static DoudizhuDeal Decode(string encoded)
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
        if (all.Distinct().Count() != Card.DeckSize)
        {
            throw new FormatException(
                $"A deal must use all {Card.DeckSize} distinct cards; got {all.Distinct().Count()}.");
        }

        return new DoudizhuDeal(hands, kitty);
    }

}
