using System;
using System.Collections.Generic;
using System.Linq;

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
        var deck = Card.FullDeck.ToArray();

        // Fisher–Yates,从后往前 —— 与 TetrisPieceSequence 同一个洗法、同一个理由:
        // **算法写死在这里,不用运行时的 RNG。** `System.Random` 的算法在 .NET 版本之间
        // 变过,而这副牌必须在任何运行时上都发得一模一样,否则升级一次运行时,
        // 所有历史对局的重放都会读出别的牌。
        var state = unchecked((uint)seed);
        if (state == 0)
        {
            // 状态 0 会让 xorshift 永远停在 0。
            //
            // **我先把这里的后果写错了。** 原注释说"那会退化成永远不洗" —— 不对:状态恒为 0
            // 时每次的 `j` 都是 0,于是每一步都跟 0 号位交换,得到的是**一个与牌无关的固定置换**。
            // 牌确实动了,54 张也还各一次,所以"没洗"那种一眼可见的症状不会出现。真正的后果是
            // **熵全丢**:任何落到零状态的种子发出的是同一副牌。
            //
            // 这个区别是变异测试指出来的:把这行改成 `state = 0`,我原本那条断言
            // (第一手不等于牌堆前 17 张)照样绿。现在钉的是那条精确的性质 ——
            // `FromSeed(0)` 必须与直接给出这个常数的种子发出同一副牌。
            //
            // 与 TetrisPieceSequence 用的是同一个替代常数。
            state = 0x9E3779B9;
        }

        for (var i = deck.Length - 1; i > 0; i--)
        {
            state = NextState(state);
            var j = (int)(state % (uint)(i + 1));
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

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

    /// <summary>xorshift32 —— 与 <c>TetrisPieceSequence</c> 同一个实现,同一个理由。</summary>
    private static uint NextState(uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }
}
