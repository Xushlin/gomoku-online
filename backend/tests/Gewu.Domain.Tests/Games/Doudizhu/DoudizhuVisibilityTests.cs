using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Doudizhu;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.Doudizhu;

/// <summary>
/// 每个座位**看得到什么**。
/// <para>
/// 这一条是斗地主唯一一处"泄漏了就完了"的地方:手牌。所以这里的断言不是"视图里有我的牌",
/// 而是**"视图里没有别人的任何一张牌"** —— 前者在一个把三家手牌都塞进去的实现上也会绿。
/// </para>
/// </summary>
public class DoudizhuVisibilityTests
{
    private const int Seed = 20260819;

    private static readonly DoudizhuRules Rules = new();

    private static MatchState Dealt(params string[] moves)
    {
        var history = moves
            .Select((text, i) => PlayedMove.Said(text, i % DoudizhuDeal.SeatCount))
            .ToList();
        return new MatchState(Rules.CreateSetup(Seed), history);
    }

    private static JsonElement View(MatchState state, int? seat)
        => JsonDocument.Parse(Rules.ViewFor(state, seat)).RootElement;

    private static string Hand(JsonElement view) => view.GetProperty("myHand").GetString()!;

    [Fact]
    public void A_seat_sees_its_own_hand()
    {
        var state = Dealt();

        var hand = Card.DecodeMany(Hand(View(state, 0)));

        hand.Should().HaveCount(DoudizhuDeal.HandSize);
        hand.Should().BeEquivalentTo(DoudizhuTable.Reconstruct(state).HandOf(0));
    }

    [Fact]
    public void No_seat_sees_a_single_card_belonging_to_anyone_else()
    {
        // **这条是整个变更的意义所在。** 逐张比对,而不是数张数:一个把三家牌都塞进去的实现
        // 在"我看得到我的 17 张"那条断言下是绿的。
        var state = Dealt();
        var table = DoudizhuTable.Reconstruct(state);

        for (var seat = 0; seat < DoudizhuDeal.SeatCount; seat++)
        {
            var visible = Card.DecodeMany(Hand(View(state, seat))).ToHashSet();

            for (var other = 0; other < DoudizhuDeal.SeatCount; other++)
            {
                if (other == seat)
                {
                    continue;
                }

                foreach (var card in table.HandOf(other))
                {
                    visible.Should().NotContain(card,
                        $"seat {seat} must not see any card from seat {other}");
                }
            }
        }
    }

    [Fact]
    public void Someone_with_no_seat_sees_no_hand_at_all()
    {
        // 围观者与"进了房间还没入座"的人都走这条路 —— 他们拿到的是空串,而不是某一家的牌。
        var state = Dealt();

        Hand(View(state, null)).Should().BeEmpty();

        // 反面控制:座位号越界的也一样(内核不该把一个坏座位号变成"看别人的牌")。
        Hand(View(state, DoudizhuDeal.SeatCount)).Should().BeEmpty();
        Hand(View(state, -1)).Should().BeEmpty();
    }

    [Fact]
    public void Everyone_sees_everyone_elses_card_counts()
    {
        // 张数是牌桌上看得见的东西 —— 藏它不会更安全,只会让"对家只剩两张了"这个决定性的
        // 信息画不出来。
        var state = Dealt();

        foreach (var seat in new int?[] { 0, 1, 2, null })
        {
            var counts = View(state, seat).GetProperty("handCounts").EnumerateArray()
                .Select(e => e.GetInt32()).ToList();
            counts.Should().Equal(Enumerable.Repeat(DoudizhuDeal.HandSize, DoudizhuDeal.SeatCount));
        }
    }

    [Fact]
    public void The_kitty_is_hidden_while_bidding_and_public_once_the_landlord_is_known()
    {
        // 底牌决定了谁值得抢地主,所以叫分阶段给出去就是给了不该有的信息;
        // 而地主当众把它收进手里之后,它就是三家都该知道的事。
        var bidding = Dealt();
        View(bidding, 0).GetProperty("kitty").ValueKind.Should().Be(JsonValueKind.Null);

        var decided = Dealt("bid:3");
        var kitty = View(decided, 1).GetProperty("kitty").GetString();

        kitty.Should().NotBeNull();
        Card.DecodeMany(kitty!).Should().HaveCount(DoudizhuDeal.KittySize)
            .And.BeEquivalentTo(DoudizhuTable.Reconstruct(decided).Kitty);

        // 而地主手上因此是 20 张,不是 17 —— 底牌进了他的手。
        Card.DecodeMany(Hand(View(decided, 0))).Should()
            .HaveCount(DoudizhuDeal.HandSize + DoudizhuDeal.KittySize);
    }

    [Fact]
    public void The_kitty_stays_hidden_while_someone_has_bid_but_the_bidding_is_not_over()
    {
        // **上面那条测试有一个盲点,而它与挖坑那条一模一样。** 它用「一步都没走」验隐藏、
        // 用 `bid:3` 验公开 —— 而 `bid:3` **立刻**结束叫分,所以「有人叫过分、但叫分还没结束」
        // 那一格从来没有被走到。
        //
        // 而 `ViewFor` 当时的判据是 `Landlord is null`,`Landlord` 在有人叫过一次分的那一刻
        // 就非空(它是「当前最高叫分者」)。于是首家叫 1 分,三张底牌就对所有人公开了。
        //
        // 这个缺陷是在挖坑的界面上被用户抓到的 —— 而**两个牌类棋种各写了一遍那一行,
        // 所以各错了一遍**。
        var state = Dealt("bid:1");

        DoudizhuTable.Reconstruct(state).Landlord.Should().NotBeNull(
            "前提:这一步之后确实有一个「当前最高叫分者」");
        DoudizhuTable.Reconstruct(state).Phase.Should().Be(
            DoudizhuPhase.Bidding, "而叫分还没结束");

        foreach (int? seat in new int?[] { null, 0, 1, 2 })
        {
            View(state, seat).GetProperty("kitty").ValueKind.Should().Be(
                JsonValueKind.Null, $"seat {seat} MUST NOT 在叫分未结束时看到底牌");
        }
    }

    [Fact]
    public void The_hand_on_the_table_is_public_together_with_who_played_it()
    {
        var state = Dealt("bid:3");
        var landlordHand = DoudizhuTable.Reconstruct(state).HandOf(0);
        var played = Card.Encode([landlordHand[0]]);

        var after = new MatchState(state.Setup, [
            .. state.History,
            PlayedMove.Said($"play:{played}", 0),
        ]);

        foreach (var seat in new int?[] { 0, 1, 2, null })
        {
            var view = View(after, seat);
            view.GetProperty("tableSeat").GetInt32().Should().Be(0);
            view.GetProperty("tableCards").GetString().Should().Be(played);
        }
    }

    [Fact]
    public void The_view_is_a_pure_function_of_state_and_seat()
    {
        // 纯函数,所以"某个座位看得到什么"是可断言的,而不是取决于调用时机。
        var state = Dealt("bid:2");

        Rules.ViewFor(state, 1).Should().Be(Rules.ViewFor(state, 1));
        Rules.ViewFor(state, 1).Should().NotBe(Rules.ViewFor(state, 2),
            "两个座位的手牌不同,所以两份视图必须不同 —— 否则裁剪根本没发生");
    }

    [Fact]
    public void The_phase_and_the_landlord_are_public()
    {
        var bidding = View(Dealt(), null);
        bidding.GetProperty("phase").GetString().Should().Be("Bidding");
        bidding.GetProperty("landlord").ValueKind.Should().Be(JsonValueKind.Null);
        bidding.GetProperty("baseScore").GetInt32().Should().Be(0);

        // 三家都叫过之后叫分才结束 —— 一个 `bid:2` 还在叫分阶段,而**第一版这条测试就写错在这里**,
        // 它假设叫一次就定地主。0 号叫 2、另两家不叫,于是底分 2、地主是 0 号。
        var playing = View(Dealt("bid:2", "bid:0", "bid:0"), null);
        playing.GetProperty("phase").GetString().Should().Be("Playing");
        playing.GetProperty("landlord").GetInt32().Should().Be(0);
        playing.GetProperty("baseScore").GetInt32().Should().Be(2);
    }

    [Fact]
    public void The_payload_is_camel_cased_json()
    {
        // 与平台上其它 JSON 载荷一致。客户端按名字读,所以这是契约的一部分。
        var raw = Rules.ViewFor(Dealt(), 0);

        raw.Should().StartWith("{").And.Contain("\"myHand\"").And.Contain("\"handCounts\"");
        raw.Should().NotContain("\"MyHand\"");
    }
}
