using System.Linq;
using FluentAssertions;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Doudizhu;

namespace Gewu.Domain.Tests.Games.Doudizhu;

/// <summary>
/// 一步斗地主在 <c>Move.Text</c> 里的编码。
/// <para>
/// 标签存在的全部理由是一条歧义:牌的字母表是 <c>A-Za-z@#</c>,而 <c>p</c> / <c>a</c> / <c>s</c>
/// 都是合法的牌字符 —— 一个裸的 <c>"pass"</c> **就是一手四张牌的合法编码**。
/// </para>
/// </summary>
public class DoudizhuMoveTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A_bid_round_trips(int points)
    {
        var encoded = DoudizhuMove.Bidding(points).Encode();

        var parsed = DoudizhuMove.Parse(encoded);

        parsed.Kind.Should().Be(DoudizhuMoveKind.Bid);
        parsed.Bid.Should().Be(points);
    }

    [Fact]
    public void A_pass_round_trips()
    {
        DoudizhuMove.Parse(DoudizhuMove.Passing().Encode())
            .Kind.Should().Be(DoudizhuMoveKind.Pass);
    }

    [Fact]
    public void A_play_round_trips()
    {
        var cards = DoudizhuDeal.FromSeed(20260819).Hands[0].Take(3).ToList();

        var parsed = DoudizhuMove.Parse(DoudizhuMove.Playing(cards).Encode());

        parsed.Kind.Should().Be(DoudizhuMoveKind.Play);
        parsed.Cards.Should().Equal(cards);
    }

    [Fact]
    public void A_bare_word_can_be_a_legal_hand_of_cards()
    {
        // **这条是那个标签存在的全部理由。** 牌的字母表是 `A-Za-z@#`,所以一个由字母组成的
        // 英文词就是一手合法的牌 —— 没有标签的话,"这是动作还是牌"没法判。
        Card.DecodeMany("cab").Should().HaveCount(3, "三个字符每个都是合法的牌");

        // **第一版这条测试写的是 "pass",而它是错的 —— 不是错在结论,是错在理由。**
        // "pass" 里有两个 `s`,而 `Card.DecodeMany` 恰好拒绝重复的牌,所以那一串会抛。
        // 也就是说 "pass" 的安全**是运气**(那个词刚好有重复字母),不是标签的功劳。
        // 换成 "paw"、"cab" 这样没有重复字母的词,歧义就真的在那里。
        var ambiguous = () => Card.DecodeMany("pass");
        ambiguous.Should().Throw<System.FormatException>("'pass' 恰好有两个 s —— 这是运气");

        DoudizhuMove.Parse("pass").Kind.Should().Be(DoudizhuMoveKind.Pass);
        DoudizhuMove.Parse("play:cab").Cards.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bid:4")]
    [InlineData("bid:-1")]
    [InlineData("bid:")]
    [InlineData("bid:12")]
    [InlineData("bid: 2")]
    [InlineData("play:")]
    [InlineData("playABC")]
    [InlineData("fold")]
    [InlineData("PASS")]
    [InlineData("ABC")]
    public void Unrecognised_text_is_refused(string text)
    {
        var act = () => DoudizhuMove.Parse(text);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_malformed_card_list_is_a_domain_refusal_not_a_crash()
    {
        // **这条测试逮到一条真缺陷。** `Card.DecodeMany` 对"不认识的字符"与"同一张牌两次"都抛
        // `FormatException`,而那不是 `DomainException` —— 于是畸形载荷会以未映射异常冒出去,
        // 变成 500,而客户端看到的是"服务器出错了",实际上是它自己发错了。
        //
        // 顺带发现:`Parse` 里我原来写的那条"同一张牌不能出现两次"是**死代码**,`DecodeMany`
        // 早就在拦。与 add-doudizhu-cards 里的 `WingsAreLegal` 同一个缺陷。
        var duplicate = Card.SmallJoker.Encode();

        foreach (var bad in new[] { $"play:{duplicate}{duplicate}", "play:!!!", "play:一" })
        {
            var act = () => DoudizhuMove.Parse(bad);
            act.Should().Throw<InvalidMoveException>($"'{bad}' 该是一次领域拒绝");
        }
    }

    [Fact]
    public void A_full_landlord_hand_fits_in_the_text_column()
    {
        // Move.Text 的上限是 64。地主手上最多 20 张,`play:` 五个字符 —— 25。
        // 这个数字是 add-doudizhu-cards 决定"不加第四种载荷"的依据,所以它值得钉住。
        var deal = DoudizhuDeal.FromSeed(20260819);
        var twenty = deal.Hands[0].Concat(deal.Kitty).ToList();

        twenty.Should().HaveCount(20);
        DoudizhuMove.Playing(twenty).Encode().Length.Should().Be(25).And.BeLessThan(64);
    }
}
