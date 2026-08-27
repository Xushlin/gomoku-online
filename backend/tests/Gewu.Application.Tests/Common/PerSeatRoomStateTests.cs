using System.Text.Json;
using Gewu.Application.Common.Mapping;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Doudizhu;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Xiangqi;
using Gewu.Domain.Games.NInARow;
using Gewu.Application.Tests.Features.Rooms;

namespace Gewu.Application.Tests.Common;

/// <summary>
/// 房间快照**按座位**裁剪。
/// <para>
/// 这一层验的是"裁剪确实发生在 DTO 上",而不是"规则会裁剪"(那是
/// <c>DoudizhuVisibilityTests</c> 的事)。两层分开,因为此前这条路上每一段都可能把它丢掉:
/// 规则算对了、`RoomView` 没带上、`ToState` 没写进 DTO —— 任何一段断掉,症状都是
/// "客户端看不到自己的牌",而看起来像同一个 bug。
/// </para>
/// </summary>
public class PerSeatRoomStateTests
{
    private static readonly DoudizhuRules Rules = new();

    private static readonly IReadOnlyDictionary<Guid, string> NoNames = new Dictionary<Guid, string>();

    private static (Room Room, UserId[] Players) DoudizhuRoom()
    {
        var host = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "ddz", host, RoomsFixtures.Now, GameKeys.Doudizhu);
        var second = UserId.NewId();
        var third = UserId.NewId();
        room.JoinAsPlayer(second, RoomsFixtures.Now.AddSeconds(1), Rules, setup: null);
        room.JoinAsPlayer(third, RoomsFixtures.Now.AddSeconds(2), Rules, setup: Rules.CreateSetup(20260819));
        return (room, [host, second, third]);
    }

    private static string? SeatViewOf(Room room, RoomView view)
        => room.ToState(NoNames, 60, view).Game?.SeatView;

    [Fact]
    public void Every_seat_gets_a_different_snapshot()
    {
        var (room, _) = DoudizhuRoom();

        var views = Enumerable.Range(0, Rules.SeatCount)
            .Select(seat => SeatViewOf(room, RoomView.ForSeat(room, seat, Rules)))
            .ToList();

        views.Should().OnlyContain(v => v != null);
        views.Should().OnlyHaveUniqueItems("三家手牌不同,所以三份快照必须不同 —— 相同就意味着裁剪没发生");
    }

    [Fact]
    public void A_player_sees_their_own_hand_through_the_dto()
    {
        var (room, players) = DoudizhuRoom();

        var mine = SeatViewOf(room, RoomView.For(room, players[2], Rules));

        mine.Should().NotBeNull();
        var hand = JsonDocument.Parse(mine!).RootElement.GetProperty("myHand").GetString();
        Card.DecodeMany(hand!).Should().BeEquivalentTo(
            DoudizhuTable.Reconstruct(room.Game!.State()).HandOf(2));
    }

    [Fact]
    public void Spectators_and_observers_get_no_hand_through_the_dto()
    {
        var (room, _) = DoudizhuRoom();

        foreach (var view in new[] { RoomView.ForSpectators(room, Rules), RoomView.ForObservers(room, Rules) })
        {
            var seen = SeatViewOf(room, view);
            seen.Should().NotBeNull("公开信息(阶段、张数、桌面)还是要给的");
            JsonDocument.Parse(seen!).RootElement.GetProperty("myHand").GetString()
                .Should().BeEmpty();
        }
    }

    [Fact]
    public void A_game_with_no_hidden_state_carries_no_seat_view()
    {
        // 四个既有棋种一行不动,所以它们的快照上这个字段 MUST 是 null —— 不是空对象、不是空串。
        // 空串会让客户端以为"有私有状态,只是空的"。
        var host = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "gomoku", host, RoomsFixtures.Now, GameKeys.Gomoku);
        room.JoinAsPlayer(UserId.NewId(), RoomsFixtures.Now.AddSeconds(1), BuiltInGameRules.Gomoku, setup: null);

        SeatViewOf(room, RoomView.ForSeat(room, 0, BuiltInGameRules.Gomoku)).Should().BeNull();
        SeatViewOf(room, RoomView.For(room, host, BuiltInGameRules.Gomoku)).Should().BeNull();
    }

    [Fact]
    public void A_waiting_room_has_no_seat_view_even_for_a_hidden_state_game()
    {
        // 还没开局就没有发牌,规则无从重建局面。这不是防御性判空 —— 大厅里每个等待中的
        // 斗地主房间都会走到这里,而一个抛异常的投影会让房间列表整页挂掉。
        var host = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "ddz", host, RoomsFixtures.Now, GameKeys.Doudizhu);

        var act = () => room.ToState(NoNames, 60, RoomView.ForSeat(room, 0, Rules));

        act.Should().NotThrow();
        act().Game.Should().BeNull();
    }

    [Fact]
    public void The_seats_are_on_the_dto_including_the_third_one()
    {
        // `Black` / `White` 是 0 号与 1 号的派生读法,所以三座位房间里 2 号座位上的人
        // **在任何字段里都不出现** —— 这是实测过的,而 `Seats` 是它的修法。
        var (room, players) = DoudizhuRoom();

        var dto = room.ToState(NoNames, 60, RoomView.ForObservers(room, Rules));

        dto.Seats.Select(s => s.Index).Should().Equal([0, 1, 2]);
        dto.Seats.Select(s => s.Player.Id).Should().Equal(players.Select(p => p.Value));
        dto.White!.Id.Should().Be(players[1].Value, "两座位的读法仍然成立,只是不完整");
    }

    [Fact]
    public void A_two_seat_room_still_lists_its_two_seats()
    {
        var host = UserId.NewId();
        var white = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "gomoku", host, RoomsFixtures.Now, GameKeys.Gomoku);
        room.JoinAsPlayer(white, RoomsFixtures.Now.AddSeconds(1), BuiltInGameRules.Gomoku, setup: null);

        var dto = room.ToState(NoNames, 60, RoomView.ForObservers(room, BuiltInGameRules.Gomoku));

        dto.Seats.Should().HaveCount(2);
        dto.Seats[0].Player.Id.Should().Be(host.Value);
        dto.Seats[1].Player.Id.Should().Be(white.Value);
    }

    /// <summary>
    /// **发牌的棋种,那副牌一个字都不许出现在 <c>ChosenSetup</c> 里。**
    /// <para>
    /// 这条守的是新加的那个字段。它下发是安全的,而担保来自「设置有两个落点」这件结构性事实:
    /// 发牌走 <c>Game.Setup</c>(本 DTO 不下发),选定式走 <c>Room.ChosenSetup</c>(下发)。
    /// 哪天有人把两者合并成一个字段,这条会红 —— 而那正是该问「这样一来牌会不会出去」的时刻。
    /// </para>
    /// <para>
    /// 三个座位都查,而不是只查一个:一次「只裁剪了当前座位」的实现在单座位断言上是绿的。
    /// </para>
    /// </summary>
    [Fact]
    public void A_dealt_game_never_exposes_its_deal_as_a_chosen_setup()
    {
        var (room, _) = DoudizhuRoom();

        // 前提:这一局**确实有**一副牌。否则下面那条在「什么都没有」上恒真。
        room.Game!.Setup.Should().NotBeNullOrEmpty();

        for (var seat = 0; seat < Rules.SeatCount; seat++)
        {
            room.ToState(NoNames, 60, RoomView.ForSeat(room, seat, Rules))
                .ChosenSetup.Should().BeNull("座位 {0} 也不该从这个字段看到那副牌", seat);
        }

        room.ToState(NoNames, 60, RoomView.ForSpectators(room, Rules)).ChosenSetup.Should().BeNull();
    }

    /// <summary>
    /// 反面对照:选定式的房间**确实**通过这个字段把局面下发出去 —— 否则上一条在
    /// 「这个字段永远是 null」上恒真,而那样客户端画不出残局。
    /// </summary>
    [Fact]
    public void A_positional_room_does_carry_its_chosen_position()
    {
        var rules = (IPositionalStartRules)BuiltInGameRules
            .All(GomokuRules.Lexicon).Single(r => r is IPositionalStartRules);
        var setup = new XiangqiSetup(StandardBoard, FirstSeat: 1).Encode();
        var room = Room.CreateFromPosition(
            RoomId.NewId(), "endgame", UserId.NewId(), RoomsFixtures.Now, rules, setup);

        room.Status.Should().Be(RoomStatus.Waiting);
        room.ToState(NoNames, 60, RoomView.ForSpectators(room, rules))
            .ChosenSetup.Should().Be(setup, "等待中的房间也要给 —— 房主要看见自己刚摆的那一局");
    }

    /// <summary>标准开局的盘面串 —— 一个**合法**的局面就够,这条测的是字段有没有传下去。</summary>
    private const string StandardBoard =
        "rnbakabnr..........c.....c.p.p.p.p.p..................P.P.P.P.P.C.....C..........RNBAKABNR";
}
