using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Wakeng;
using Gewu.Domain.Idioms;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.Wakeng;

/// <summary>
/// 每个座位**看得到什么**。
/// <para>
/// 挖坑有两处「泄漏了就完了」的地方:手牌,以及叫分阶段的底牌。所以这里的断言不是
/// 「视图里有我的牌」,而是**「视图里没有别人的任何一张牌」** —— 前者在一个把三家手牌都塞进去
/// 的实现上也会绿。
/// </para>
/// </summary>
public class WakengVisibilityTests
{
    private const int Seed = 20260820;

    private static readonly WakengRules Rules = new();

    private static readonly string Setup = Rules.CreateSetup(Seed);

    private static int First => WakengDeal.Decode(Setup).FirstBidder().Seat;

    private static MatchState Dealt(params string[] moves)
    {
        var history = moves
            .Select((text, i) => PlayedMove.Said(text, (First + i) % WakengDeal.SeatCount))
            .ToList();
        return new MatchState(Setup, history);
    }

    private static JsonElement View(MatchState state, int? seat)
        => JsonDocument.Parse(Rules.ViewFor(state, seat)).RootElement;

    private static string Hand(JsonElement view) => view.GetProperty("myHand").GetString()!;

    [Fact]
    public void A_seat_sees_its_own_hand()
    {
        var state = Dealt();

        var hand = Card.DecodeMany(Hand(View(state, 0)));

        hand.Should().HaveCount(WakengDeal.HandSize);
        hand.Should().BeEquivalentTo(WakengTable.Reconstruct(state).HandOf(0));
    }

    [Fact]
    public void No_seat_sees_a_single_card_belonging_to_anyone_else()
    {
        // **这条是整个接缝的意义所在。** 逐张比对,而不是数张数:一个把三家牌都塞进去的实现
        // 在「我看得到我的 16 张」那条断言下是绿的。
        var state = Dealt();
        var table = WakengTable.Reconstruct(state);

        for (var seat = 0; seat < WakengDeal.SeatCount; seat++)
        {
            var visible = Card.DecodeMany(Hand(View(state, seat))).ToHashSet();

            for (var other = 0; other < WakengDeal.SeatCount; other++)
            {
                if (other == seat)
                {
                    continue;
                }

                foreach (var card in table.HandOf(other))
                {
                    visible.Should().NotContain(card,
                        $"座位 {seat} MUST NOT 看到座位 {other} 的 {card.Encode()}");
                }
            }
        }
    }

    [Fact]
    public void A_spectator_and_an_out_of_range_seat_both_see_no_hand()
    {
        // 负控制:**一个坏的座位号 MUST NOT 变成别人的牌**。
        foreach (int? seat in new int?[] { null, -1, 3, 99 })
        {
            var view = View(Dealt(), seat);

            Hand(view).Should().BeEmpty($"seat {seat} 不占座位");
            // 而公开信息仍在 —— 围观者要画得出这张桌子。
            view.GetProperty("handCounts").GetArrayLength().Should().Be(WakengDeal.SeatCount);
            view.GetProperty("phase").GetString().Should().Be("Bidding");
            view.GetProperty("firstBidderCard").GetString().Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void The_kitty_is_hidden_until_the_digger_is_decided()
    {
        // 叫分阶段它 MUST 为 null —— 那时它还没被翻开,而它恰恰决定了这一局值不值得挖。
        foreach (int? seat in new int?[] { null, 0, 1, 2 })
        {
            View(Dealt(), seat).GetProperty("kitty").ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public void The_kitty_stays_hidden_while_someone_has_bid_but_the_bidding_is_not_over()
    {
        // **这一条是用户在屏幕上抓到的那个缺陷的可执行形式。**
        //
        // 上面那条「叫分阶段底牌为 null」用的是**一步都没走**的局面,于是 `Digger` 本来就是
        // null —— 它一直因为**别的理由**通过。而 `ViewFor` 当时的判据是 `Digger is null`,
        // 而 `Digger` 在**有人叫过一次分**的那一刻就非空(它的含义是「当前最高叫分者」)。
        //
        // 于是首家一叫,四张底牌就对所有人公开了 —— 而后面两家正是靠看不见它才要下判断。
        var state = Dealt("bid:1");

        WakengTable.Reconstruct(state).Digger.Should().NotBeNull(
            "前提:这一步之后确实有一个「当前最高叫分者」,否则这条断言什么都不验");
        WakengTable.Reconstruct(state).Phase.Should().Be(
            WakengPhase.Bidding, "而叫分还没结束");

        foreach (int? seat in new int?[] { null, 0, 1, 2 })
        {
            View(state, seat).GetProperty("kitty").ValueKind.Should().Be(
                JsonValueKind.Null, $"seat {seat} MUST NOT 在叫分未结束时看到底牌");
        }
    }

    [Fact]
    public void The_kitty_is_public_to_everyone_once_the_digger_is_decided()
    {
        var state = Dealt("bid:3");
        var expected = Card.Encode(WakengTable.Reconstruct(state).Kitty);

        expected.Should().HaveLength(WakengDeal.KittySize);

        foreach (int? seat in new int?[] { null, 0, 1, 2 })
        {
            View(state, seat).GetProperty("kitty").GetString().Should().Be(expected);
        }
    }

    [Fact]
    public void The_first_bidder_and_the_card_they_showed_are_public()
    {
        // **一处判断,记在这里:** 按规则那张 ♣ 本来就是明示的(它决定了谁首叫首出),
        // 而服务端算得出 —— 客户端不该自己猜。
        var (seat, card) = WakengDeal.Decode(Setup).FirstBidder();

        foreach (int? viewer in new int?[] { null, 0, 1, 2 })
        {
            var view = View(Dealt(), viewer);
            view.GetProperty("firstBidder").GetInt32().Should().Be(seat);
            view.GetProperty("firstBidderCard").GetString().Should().Be(card.Encode().ToString());
        }
    }

    [Fact]
    public void CanFollow_answers_hand_versus_table_and_not_whose_turn_it_is()
    {
        // **定义写死在这里,因为一个「有时是 false 只因为还没轮到你」的字段会让客户端
        // 在错的时候自动过牌。** `ViewFor` 收的 `MatchState` 里根本没有当前回合。
        var state = Dealt();
        var table = WakengTable.Reconstruct(state);

        for (var seat = 0; seat < WakengDeal.SeatCount; seat++)
        {
            var expected = WakengFollows.CanFollow(table.HandOf(seat), table.Current);
            View(state, seat).GetProperty("canFollow").GetBoolean().Should().Be(
                expected, $"seat {seat} 的答案只由「手牌 × 桌面」决定");
        }
    }

    [Fact]
    public void Free_lead_means_everyone_can_follow()
    {
        // 桌面为空时一张单牌永远合法,所以只要手里还有牌它就是 true。
        var state = Dealt();

        WakengTable.Reconstruct(state).Current.Should().BeNull("前提:这时桌面是空的");
        for (var seat = 0; seat < WakengDeal.SeatCount; seat++)
        {
            View(state, seat).GetProperty("canFollow").GetBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public void Someone_with_no_seat_can_never_follow()
    {
        // 围观者没有手牌,也不该拿到一个关于别人手牌的答案 —— 恒 false,而不是「看某一家的」。
        foreach (int? seat in new int?[] { null, -1, 3, 99 })
        {
            View(Dealt(), seat).GetProperty("canFollow").GetBoolean().Should().BeFalse(
                $"seat {seat} 不占座位");
        }
    }

    [Fact]
    public void The_view_does_not_publish_the_base_score()
    {
        // 基数今天恒等于 1,而那不是这一局的*状态*,是一个还不存在的房间设置。
        // 发一个只有一个取值的字段,等于请客户端画「×1」。
        typeof(WakengSeatView).GetProperties().Select(p => p.Name)
            .Should().NotContain("BaseScore")
            .And.NotContain("Base")
            .And.NotContain("Multiplier");
    }

    [Fact]
    public void The_same_seat_asked_twice_gets_the_same_answer()
    {
        var state = Dealt();

        Rules.ViewFor(state, 0).Should().Be(Rules.ViewFor(state, 0));
        Rules.ViewFor(state, 0).Should().NotBe(Rules.ViewFor(state, 1), "裁剪必须真的发生了");
    }

    [Fact]
    public void Exactly_two_built_in_games_project_a_per_seat_view()
    {
        // **这条走查在被写出来之前从来没有存在过,而规格里早就写着它。**
        //
        // `add-doudizhu-visibility` 在 `game-rules-registry` 里留下一条 Scenario:
        // 「恰好一个内置棋种实现它,且它的 GameKey == "doudizhu"」。而 `backend/tests/` 下
        // **一次都没有出现过 `IPerSeatViewRules` 这个词** —— 用阳性对照量过(同样的搜法必须
        // 搜得到 `IDealtGameRules`,它在四个文件里)。
        //
        // 它的两个邻居(IDealtGameRules / ITimeoutFallbackRules)各有一条真断言,所以这一条
        // 读起来像也有。**一条没有实现的 Scenario 与一条错的 Scenario 在归档时长得一模一样**,
        // 而 `openspec validate --strict` 两者都放行 —— 它验的是形状,从不验真假。
        var lexicon = new InMemoryIdiomLexicon(["一心一意"]);

        BuiltInGameRules.All(lexicon).Where(r => r is IPerSeatViewRules).Select(r => r.GameKey)
            .Should().BeEquivalentTo([GameKeys.Doudizhu, GameKeys.Wakeng]);
    }
}
