using System;
using System.Collections.Generic;

namespace Gewu.Domain.Games.Cards;

/// <summary>
/// 按种子洗一副牌。**同一个种子永远洗出同一个顺序。**
/// <para>
/// 从 <c>DoudizhuDeal</c> 里提出来,因为挖坑要洗同一副牌 —— 而那会是这段
/// Fisher–Yates 加 xorshift32 的**第三份**副本(第二份在 <c>TetrisPieceSequence</c>)。
/// 这个仓库为「一份手写的东西被当成唯一来源」付过三次账;一段算法被抄三遍是同一个形状,
/// 只是它错起来更安静:两个棋种各自洗出一副合法的牌,而其中一个的零状态陷阱没被处理。
/// </para>
/// <para>
/// <c>TetrisPieceSequence</c> **刻意不改**:它那一份的存在理由是客户端必须用 TypeScript
/// 实现同一个算法,而那份 TS 已经与它逐项对齐过(三个整袋、21 个方块)。让它去依赖一个
/// 叫 <c>CardShuffle</c> 的东西,是把「方块序列」说成「洗牌」。
/// </para>
/// </summary>
public static class CardShuffle
{
    /// <summary>
    /// 零状态的替代常数。xorshift32 在 0 上会永远停在 0。
    /// <para>
    /// 与 <c>TetrisPieceSequence</c> 用的是同一个常数(黄金比例的 32 位定点)。
    /// </para>
    /// </summary>
    private const uint ZeroStateSubstitute = 0x9E3779B9;

    /// <summary>
    /// 原地洗牌。
    /// <para>
    /// **算法写死在这里,不用运行时的 RNG。** <c>System.Random</c> 的算法在 .NET 版本之间
    /// 变过,而这副牌必须在任何运行时上都洗得一模一样,否则升级一次运行时,所有历史对局的
    /// 重放都会读出别的牌。
    /// </para>
    /// <para>
    /// 状态 0 会让 xorshift 永远停在 0。**这个后果曾被写错**:不是「退化成永远不洗」——
    /// 状态恒为 0 时每次的 <c>j</c> 都是 0,于是每一步都跟 0 号位交换,得到的是一个**与牌无关
    /// 的固定置换**。牌确实动了,张数也还各一次,所以"没洗"那种一眼可见的症状不会出现。
    /// 真正的后果是**熵全丢**:任何落到零状态的种子洗出同一个顺序。
    /// 那个区别是变异测试指出来的,而钉住它的断言是
    /// 「<c>FromSeed(0)</c> 必须与直接给出这个常数的种子发出同一副牌」。
    /// </para>
    /// </summary>
    /// <typeparam name="T">牌(或任何要按种子重排的东西)。</typeparam>
    /// <param name="items">要洗的列表,**原地修改**。</param>
    /// <param name="seed">洗牌种子。</param>
    public static void Shuffle<T>(IList<T> items, int seed)
    {
        ArgumentNullException.ThrowIfNull(items);

        var state = unchecked((uint)seed);
        if (state == 0)
        {
            state = ZeroStateSubstitute;
        }

        // Fisher–Yates,从后往前。
        for (var i = items.Count - 1; i > 0; i--)
        {
            state = NextState(state);
            var j = (int)(state % (uint)(i + 1));
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>xorshift32。</summary>
    private static uint NextState(uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return state;
    }
}
