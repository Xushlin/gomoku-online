using FluentAssertions;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Cards;

namespace Gewu.Domain.Tests.Games.Cards;

/// <summary>
/// 「畸形的牌是一次领域拒绝,不是一个 <c>FormatException</c>」—— 两个牌类棋种各走一遍。
/// <para>
/// <b>它守的是一条量过的缺陷。</b> <c>Card.DecodeMany</c> 对不认识的字符和重复的牌都抛
/// <c>FormatException</c>,而那不是 <c>DomainException</c> —— 于是 <c>play:!!!</c> 会以
/// 未映射异常冒出去变成 **500**,客户端看到「服务器出错了」,而实际上是它自己发错了。
/// </para>
/// <para>
/// 斗地主自己 <c>catch</c> 过它。挖坑要写第二个解析器,而**一个需要被记得的 <c>catch</c> 会在
/// 第三个解析器那里被忘掉** —— 所以映射提成了 <see cref="CardPlay"/>,而这条测试
/// **两个游戏各走一遍**:只测共享的那个函数,不能证明两个解析器都真的在用它。
/// </para>
/// </summary>
public class CardPlayTests
{
    // 三种畸形输入,三种不同的坏法:字母表外的字符、同一张牌两次、一张牌都没有。
    public static TheoryData<string> MalformedPlays => new() { "!!!", "AA", "" };

    [Theory]
    [MemberData(nameof(MalformedPlays))]
    public void The_shared_decoder_refuses_in_the_domain_vocabulary(string body)
    {
        var act = () => CardPlay.Decode(body, $"play:{body}");

        act.Should().Throw<InvalidMoveException>()
            .Which.Code.Should().Be("invalid-move");
    }

    [Theory]
    [MemberData(nameof(MalformedPlays))]
    public void Doudizhu_refuses_a_malformed_play_without_leaking_FormatException(string body)
    {
        var act = () => Gewu.Domain.Games.Doudizhu.DoudizhuMove.Parse($"play:{body}");

        act.Should().Throw<InvalidMoveException>();
    }

    [Theory]
    [MemberData(nameof(MalformedPlays))]
    public void Wakeng_refuses_a_malformed_play_without_leaking_FormatException(string body)
    {
        var act = () => Gewu.Domain.Games.Wakeng.WakengMove.Parse($"play:{body}");

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_well_formed_play_still_decodes()
    {
        // 阳性对照。少了它,一个「什么都拒绝」的实现会让上面九条全绿。
        CardPlay.Decode("cab", "play:cab").Should().HaveCount(3);
    }

    [Fact]
    public void The_deal_decoders_still_get_a_FormatException()
    {
        // **这条映射 MUST NOT 下沉到 `Card.DecodeMany`**,而这就是理由的可执行形式:
        // 两个 `Deal.Decode` 也调它,而它们要的正是 `FormatException` —— 一份坏掉的发牌是
        // **损坏的记录**,不是一步非法的棋,它不该被报成「你这一手不合法」。
        //
        // 段数不对是最短的坏发牌:它连不到牌那一层就该拒。
        var ddz = () => Gewu.Domain.Games.Doudizhu.DoudizhuDeal.Decode("AB/CD");
        var wk = () => Gewu.Domain.Games.Wakeng.WakengDeal.Decode("AB/CD");

        ddz.Should().Throw<FormatException>();
        wk.Should().Throw<FormatException>();
    }
}
