using Gewu.Application.Common.Mapping;
using Gewu.Application.Tests.Features.Rooms;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Doudizhu;
using Gewu.Domain.Rooms;

namespace Gewu.Application.Tests.Common;

/// <summary>
/// 房间**摘要**里读得到全部在座的座位。
/// <para>
/// <b>它修的是一条量出来的缺陷。</b> `RoomSummaryDto` 只有 <c>Black</c> / <c>White</c>,
/// 而那是 0 号与 1 号座位的派生读法 —— 于是三座位房间里 2 号座位上的人在这份摘要里
/// **根本不出现**,而大厅列表读的正是这份摘要。
/// </para>
/// <para>
/// 这是同一个缺陷的第三处。前两处在房间页(<c>add-web-doudizhu</c> 修「白方走棋」、
/// <c>add-doudizhu-table-visuals</c> 修侧栏只列两个人),两次都只修了房间页 ——
/// **而大厅读的是另一个 DTO**,所以那两次对它一行影响都没有。
/// </para>
/// </summary>
public class RoomSummarySeatsTests
{
    private static readonly IReadOnlyDictionary<Guid, string> NoNames =
        new Dictionary<Guid, string>();

    private static readonly DoudizhuRules Doudizhu = new();

    /// <summary>坐满三个人的斗地主房间。</summary>
    private static (Room Room, Guid[] Players) ThreeSeatRoom()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var room = Room.Create(
            RoomId.NewId(), "ddz", host.Id, RoomsFixtures.Now, GameKeys.Doudizhu);
        var second = RoomsFixtures.NewUser("Bob");
        var third = RoomsFixtures.NewUser("Carol");

        room.JoinAsPlayer(second.Id, RoomsFixtures.Now.AddSeconds(1), Doudizhu, setup: null);
        room.JoinAsPlayer(
            third.Id, RoomsFixtures.Now.AddSeconds(2), Doudizhu, setup: Doudizhu.CreateSetup(7));

        return (room, [host.Id.Value, second.Id.Value, third.Id.Value]);
    }

    [Fact]
    public void A_three_seat_room_reports_all_three_seats_in_its_summary()
    {
        var (room, players) = ThreeSeatRoom();

        var summary = room.ToSummary(NoNames);

        summary.Seats.Select(s => s.Index).Should().Equal([0, 1, 2]);
        summary.Seats.Select(s => s.Player.Id).Should().Equal(players);
    }

    [Fact]
    public void The_third_seat_is_readable_only_through_Seats()
    {
        // **两句话同时成立,才说明这个字段是加上去的、不是把旧字段改了意思。**
        //
        // 只断言「Seats 有三项」的话,一个把 White 改成"最后一个座位"的实现同样是绿的 ——
        // 而那会静静弄坏四个两座位棋种的每一个读者。
        var (room, players) = ThreeSeatRoom();

        var summary = room.ToSummary(NoNames);

        summary.Black!.Id.Should().Be(players[0], "Black 仍然是 0 号座位");
        summary.White!.Id.Should().Be(players[1], "White 仍然只是 1 号座位");
        summary.Seats.Should().HaveCount(3, "而第三个人只在 Seats 里");
        summary.Seats[2].Player.Id.Should().Be(players[2]);
    }

    [Fact]
    public void A_two_seat_room_reports_two_seats()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var challenger = RoomsFixtures.NewUser("Bob");
        var room = RoomsFixtures.PlayingRoom(host, challenger);

        var summary = room.ToSummary(NoNames);

        summary.Seats.Select(s => s.Index).Should().Equal([0, 1]);
        summary.Seats[0].Player.Id.Should().Be(host.Id.Value);
        summary.Seats[1].Player.Id.Should().Be(challenger.Id.Value);
    }

    [Fact]
    public void A_room_nobody_has_joined_still_seats_its_host()
    {
        // 建房的人立刻占 0 号座位,所以「等待中」的房间也有一项 —— 不是空列表。
        // 大厅列表的每一行都要走这条路。
        var host = RoomsFixtures.NewUser("Alice");
        var room = RoomsFixtures.WaitingRoom(host, "a room", GameKeys.Gomoku);

        var summary = room.ToSummary(NoNames);

        summary.Seats.Should().ContainSingle().Which.Index.Should().Be(0);
    }

    [Fact]
    public void The_two_dtos_agree_on_the_seats()
    {
        // 摘要与完整状态 MUST 说同一件事。两处各写一遍投影,是给下一个人一个改一处忘一处
        // 的机会 —— 这条断言让那件事红。
        var (room, _) = ThreeSeatRoom();

        var summary = room.ToSummary(NoNames);
        var state = room.ToState(NoNames, 60, RoomView.ForObservers(room, Doudizhu));

        summary.Seats.Select(s => (s.Index, s.Player.Id))
            .Should().Equal(state.Seats.Select(s => (s.Index, s.Player.Id)));
    }
}
