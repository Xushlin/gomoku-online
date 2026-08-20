using System;
using System.Collections.Generic;
using Gewu.Domain.Exceptions;

namespace Gewu.Domain.Games.Cards;

/// <summary>
/// 把一步棋里那串牌解成牌 —— 而**畸形的输入是一次领域拒绝,不是一个 <see cref="FormatException"/>**。
/// <para>
/// <b>它存在是因为一条量过的缺陷,不是为了整洁。</b> <see cref="Card.DecodeMany"/> 对
/// "不认识的字符"和"同一张牌出现两次"都抛 <see cref="FormatException"/>,而那不是
/// <c>DomainException</c> —— 于是一个畸形的客户端载荷(<c>play:!!!</c>、<c>play:AA</c>)会以
/// 未映射异常冒出去变成 **500**,客户端看到"服务器出错了",而实际上是它自己发错了。
/// <c>add-doudizhu</c> 在 <c>DoudizhuMove.Parse</c> 里 <c>catch</c> 掉了它。
/// </para>
/// <para>
/// 挖坑要写第二个解析器,而**一个需要被记得的 <c>catch</c> 会在第三个解析器那里被忘掉**。
/// 这是两个牌类棋种之间唯一真正必要的共享:它的重复会重造那个缺陷。其余部分(牌型、大小、
/// 一步棋的类型)各自一份 —— **形状相同不等于事实相同**,而它们可以分歧
/// (挖坑哪天要 <c>bid:4</c>,斗地主一行不动)。
/// </para>
/// <para>
/// <b>这条映射 MUST 留在这一层,MUST NOT 下沉到 <see cref="Card.DecodeMany"/>。</b>
/// <c>DoudizhuDeal.Decode</c> 与 <c>WakengDeal.Decode</c> 也调它,而它们**要的正是**
/// <see cref="FormatException"/> —— 一份坏掉的发牌是**损坏的记录**,不是一步非法的棋,
/// 它不该被报成"你这一手不合法"。两个调用方要两种异常,所以映射只能在上面这一层。
/// </para>
/// </summary>
public static class CardPlay
{
    /// <summary>
    /// 解一手牌。空手也是拒绝 —— 一步"出牌"必须至少出一张。
    /// </summary>
    /// <param name="encoded">牌的编码串(不含 <c>play:</c> 这类标签)。</param>
    /// <param name="context">出现在错误消息里的东西,通常是整步棋的文本。</param>
    /// <exception cref="InvalidMoveException">认不出来,或一张牌都没有。</exception>
    public static IReadOnlyList<Card> Decode(string encoded, string context)
    {
        IReadOnlyList<Card> cards;
        try
        {
            cards = Card.DecodeMany(encoded);
        }
        catch (FormatException e)
        {
            throw new InvalidMoveException($"'{context}' does not name a legal set of cards.", e);
        }

        if (cards.Count == 0)
        {
            throw new InvalidMoveException("A play must name at least one card.");
        }

        return cards;
    }
}
