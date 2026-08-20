using System.Text.Json;
using Gewu.Application.Features.Rooms.GetPlayHints;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Wakeng;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;
using Moq;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// 候选出法的按需查询 —— 提示按钮读它,而「要不起」读 <c>seatView.canFollow</c>。
/// <para>
/// <b>两者是同一个事实的两个出口</b>,所以这里有一条断言把它们钉在一起:一条走遍每个座位、
/// 逐个比对 <c>canFollow == (plays.Count &gt; 0)</c>。它们若各算一遍,就会出现
/// 「提示说你能出、而系统已经替你过了」。
/// </para>
/// </summary>
public class GetPlayHintsQueryHandlerTests
{
    private const int Seed = 20260820;

    private static readonly WakengRules Rules = new();

    /// <summary>坐满三人、并且已经进入出牌阶段的挖坑房间。</summary>
    private static (Room Room, UserId[] Players) PlayingRoom()
    {
        var host = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "wakeng room", host, RoomsFixtures.Now, GameKeys.Wakeng);
        var second = UserId.NewId();
        var third = UserId.NewId();
        room.JoinAsPlayer(second, RoomsFixtures.Now.AddSeconds(1), Rules, setup: null);
        room.JoinAsPlayer(third, RoomsFixtures.Now.AddSeconds(2), Rules, setup: Rules.CreateSetup(Seed));

        var players = new[] { host, second, third };
        // 首叫者叫 3 分 —— 叫分立即结束,而出手权回到他自己。
        var first = WakengTable.Reconstruct(room.Game!.State()).FirstBidderSeat;
        room.PlayMove(players[first], MoveIntent.Say("bid:3"), RoomsFixtures.Now.AddSeconds(10), Rules);

        return (room, players);
    }

    private static GetPlayHintsQueryHandler Handler(Room room)
    {
        var rooms = new Mock<IRoomRepository>();
        rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);
        return new GetPlayHintsQueryHandler(rooms.Object);
    }

    private static async Task<PlayHintsDto> Hints(Room room, UserId user)
        => await Handler(room).Handle(new GetPlayHintsQuery(user, room.Id), default);

    [Fact]
    public async Task A_seated_player_gets_plays_from_their_own_hand()
    {
        var (room, players) = PlayingRoom();
        var table = WakengTable.Reconstruct(room.Game!.State());
        var seat = table.FirstBidderSeat;

        var hints = await Hints(room, players[seat]);

        hints.Plays.Should().NotBeEmpty("自由首出时手里总有牌可出");
        foreach (var play in hints.Plays)
        {
            var cards = Card.DecodeMany(play);
            cards.Should().OnlyContain(c => table.HandOf(seat).Contains(c),
                "候选只能来自这个座位自己的手牌");
            WakengCombo.TryRecognise(cards, out _).Should().BeTrue("而且必须是合法牌型");
        }
    }

    [Fact]
    public async Task It_never_answers_for_someone_elses_hand()
    {
        // **一个能查别人候选的端点,等于把别人的手牌算出来给你。**
        // 围观者与非玩家因此拿到空,而不是某一家的候选。
        var (room, _) = PlayingRoom();
        var stranger = UserId.NewId();
        room.JoinAsSpectator(stranger);

        (await Hints(room, stranger)).Plays.Should().BeEmpty("围观者");
        (await Hints(room, UserId.NewId())).Plays.Should().BeEmpty("跟这个房间无关的人");
    }

    [Fact]
    public async Task The_hints_agree_with_canFollow_on_every_seat()
    {
        // **两个出口读同一个事实,那就该有一条断言把它们钉在一起。**
        var (room, players) = PlayingRoom();
        var state = room.Game!.State();

        for (var seat = 0; seat < Rules.SeatCount; seat++)
        {
            var canFollow = JsonDocument.Parse(Rules.ViewFor(state, seat))
                .RootElement.GetProperty("canFollow").GetBoolean();
            var plays = (await Hints(room, players[seat])).Plays;

            canFollow.Should().Be(plays.Count > 0, $"seat {seat}");
        }
    }

    [Fact]
    public async Task A_non_wakeng_room_has_no_hints()
    {
        // 别的棋种返回空 —— 而那不是「你要不起」,是「这个棋种没有这个功能」。
        // 两者在客户端不会混:只有挖坑的牌桌会去按那个按钮。
        var host = UserId.NewId();
        var room = RoomsFixtures.PlayingRoom(
            RoomsFixtures.NewUser("alice"), RoomsFixtures.NewUser("bob"));

        (await Hints(room, room.HostUserId)).Plays.Should().BeEmpty();
        _ = host;
    }

    [Fact]
    public async Task The_bidding_phase_has_no_hints()
    {
        // 叫分阶段没有「出哪一手牌」这件事。
        var host = UserId.NewId();
        var room = Room.Create(RoomId.NewId(), "wakeng room", host, RoomsFixtures.Now, GameKeys.Wakeng);
        room.JoinAsPlayer(UserId.NewId(), RoomsFixtures.Now.AddSeconds(1), Rules, setup: null);
        room.JoinAsPlayer(
            UserId.NewId(), RoomsFixtures.Now.AddSeconds(2), Rules, setup: Rules.CreateSetup(Seed));

        WakengTable.Reconstruct(room.Game!.State()).Phase.Should().Be(WakengPhase.Bidding);
        (await Hints(room, host)).Plays.Should().BeEmpty();
    }

    [Fact]
    public async Task A_room_that_does_not_exist_yields_no_hints()
    {
        // 提示是一个可有可无的便利,所以「房间没了」的正确反应是没有提示,不是一条错误路径。
        var rooms = new Mock<IRoomRepository>();
        rooms.Setup(r => r.FindByIdAsync(It.IsAny<RoomId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Room?)null);

        var hints = await new GetPlayHintsQueryHandler(rooms.Object)
            .Handle(new GetPlayHintsQuery(UserId.NewId(), RoomId.NewId()), default);

        hints.Plays.Should().BeEmpty();
    }
}
