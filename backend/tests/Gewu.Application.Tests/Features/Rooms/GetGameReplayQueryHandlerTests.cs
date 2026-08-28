using Gewu.Application.Common.Mapping;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Doudizhu;
using Gewu.Application.Features.Rooms.GetGameReplay;
using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;
using Move = Gewu.Domain.ValueObjects.Move;

namespace Gewu.Application.Tests.Features.Rooms;

public class GetGameReplayQueryHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();

    private GetGameReplayQueryHandler Build() => new(_rooms.Object, _users.Object);

    /// <summary>
    /// 构造一个 Finished 房间:Alice(host/black) + Bob(white),Alice 黑方连五胜;
    /// 共落 9 步。
    /// </summary>
    private static Room FinishedRoom(User alice, User bob)
    {
        var room = Room.Create(RoomId.NewId(), "replay-test", alice.Id, RoomsFixtures.Now, GameKeys.Gomoku);
        room.JoinAsPlayer(bob.Id, RoomsFixtures.Now.AddSeconds(1), BuiltInGameRules.Gomoku, setup: null);

        // Alice (黑) 在第 7 行连五,Bob 在第 0 行被动应对
        var start = RoomsFixtures.Now.AddSeconds(2);
        for (var i = 0; i < 4; i++)
        {
            room.PlayMove(alice.Id, MoveIntent.Place(new Position(7, i)), start.AddSeconds(i * 2), BuiltInGameRules.Gomoku);
            room.PlayMove(bob.Id, MoveIntent.Place(new Position(0, i)), start.AddSeconds(i * 2 + 1), BuiltInGameRules.Gomoku);
        }
        room.PlayMove(alice.Id, MoveIntent.Place(new Position(7, 4)), start.AddSeconds(9), BuiltInGameRules.Gomoku);
        return room;
    }

    /// <summary>
    /// 一局**真的**打完的斗地主:三个座位坐满,地主把 20 张牌一张一张出光。
    /// <para>
    /// 三座位样本不能用「造一个假 Room」凑 —— 这里要证的正是 handler 从真聚合里读座位,
    /// 而一个手工塞进去的座位列表会把 <c>Room.Seats</c> 这一环跳过去。出牌脚本抄自
    /// <c>DoudizhuThroughRoomTests</c>:过牌总是合法,所以它不依赖那副牌里谁能压谁。
    /// </para>
    /// </summary>
    private static (Room Room, User[] Users) FinishedDoudizhuRoom()
    {
        var rules = new DoudizhuRules();
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var carol = RoomsFixtures.NewUser("Carol", "carol@example.com");
        var room = Room.Create(RoomId.NewId(), "ddz-replay", alice.Id, RoomsFixtures.Now, GameKeys.Doudizhu);
        room.JoinAsPlayer(bob.Id, RoomsFixtures.Now.AddSeconds(1), rules, setup: null);
        room.JoinAsPlayer(carol.Id, RoomsFixtures.Now.AddSeconds(2), rules, setup: rules.CreateSetup(20260819));

        var t = 10;
        room.PlayMove(alice.Id, MoveIntent.Say("bid:3"), RoomsFixtures.Now.AddSeconds(t++), rules);
        // 地主手上是 17 张 + 3 张底牌 = 20 张。写成两个常量相加而不是字面量 20:
        // 一个字面量在牌数改动时不会红,只会**打不完**,而症状是「测试卡在中间」。
        const int landlordCards = DoudizhuDeal.HandSize + DoudizhuDeal.KittySize;
        for (var played = 0; played < landlordCards; played++)
        {
            var hand = DoudizhuTable.Reconstruct(room.Game!.State()).HandOf(0);
            room.PlayMove(alice.Id, MoveIntent.Say($"play:{hand[0].Encode()}"), RoomsFixtures.Now.AddSeconds(t++), rules);
            if (played == landlordCards - 1) break;
            room.PlayMove(bob.Id, MoveIntent.Say("pass"), RoomsFixtures.Now.AddSeconds(t++), rules);
            room.PlayMove(carol.Id, MoveIntent.Say("pass"), RoomsFixtures.Now.AddSeconds(t++), rules);
        }

        return (room, [alice, bob, carol]);
    }

    [Fact]
    public async Task Success_Returns_Replay_With_Ordered_Moves_And_Usernames()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = FinishedRoom(alice, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var dto = await Build().Handle(new GetGameReplayQuery(room.Id), default);

        dto.RoomId.Should().Be(room.Id.Value);
        dto.Name.Should().Be("replay-test");
        dto.Seats.Should().HaveCount(2);
        dto.Seats[0].Index.Should().Be(0);
        dto.Seats[0].Player.Id.Should().Be(alice.Id.Value);
        dto.Seats[0].Player.Username.Should().Be("Alice");
        dto.Seats[1].Index.Should().Be(1);
        dto.Seats[1].Player.Id.Should().Be(bob.Id.Value);
        dto.Seats[1].Player.Username.Should().Be("Bob");
        dto.Host.Id.Should().Be(alice.Id.Value);
        dto.Result.Should().Be(GameResult.Decided);
        dto.WinnerUserId.Should().Be(alice.Id.Value);
        dto.EndReason.Should().Be(GameEndReason.Decided);
        dto.Moves.Should().HaveCount(9);
        // Moves 按 Ply 升序
        dto.Moves.Select(m => m.Ply).Should().BeInAscendingOrder();
        dto.Moves[0].Ply.Should().Be(1);
        dto.Moves[8].Ply.Should().Be(9);
    }

    [Fact]
    public async Task Room_Not_Found_Throws_RoomNotFound()
    {
        var roomId = RoomId.NewId();
        _rooms.Setup(r => r.FindByIdAsync(roomId, It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);

        var act = () => Build().Handle(new GetGameReplayQuery(roomId), default);

        await act.Should().ThrowAsync<RoomNotFoundException>();
    }

    [Fact]
    public async Task Playing_Room_Throws_GameNotFinished()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(alice, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var act = () => Build().Handle(new GetGameReplayQuery(room.Id), default);

        await act.Should().ThrowAsync<GameNotFinishedException>();
    }

    [Fact]
    public async Task Waiting_Room_Throws_GameNotFinished()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var room = RoomsFixtures.WaitingRoom(alice);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var act = () => Build().Handle(new GetGameReplayQuery(room.Id), default);

        await act.Should().ThrowAsync<GameNotFinishedException>();
    }

    [Fact]
    public async Task Three_seat_replay_names_every_one_of_the_three()
    {
        // 修之前:handler 无条件读 `BlackPlayerId` / `WhitePlayerId`,于是 2 号座位上的人
        // **在任何字段里都不出现**,而端点 200 成功返回 —— 一份丢了一个人的回放。
        var (room, users) = FinishedDoudizhuRoom();

        // 正面控制:少了这三条,「Carol 不在响应里」可能是夹具没坐满而不是 handler 丢人 ——
        // 两者长得一模一样。
        room.Status.Should().Be(RoomStatus.Finished);
        room.Seats.Should().HaveCount(3);
        room.CollectUserIds().Should().Contain(users[2].Id.Value);

        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        RoomsFixtures.SetupUserLookup(_users, users);

        var dto = await Build().Handle(new GetGameReplayQuery(room.Id), default);

        // **恰好三条**,不是「至少两条」:后者在丢掉一个座位之后依然是绿的,而那正是
        // 这个缺陷活到今天没被任何测试发现的原因。
        dto.Seats.Should().HaveCount(3);
        dto.Seats.Select(s => s.Index).Should().Equal(0, 1, 2);
        dto.Seats.Select(s => s.Player.Id).Should().Equal(
            users[0].Id.Value, users[1].Id.Value, users[2].Id.Value);
        dto.Seats.Select(s => s.Player.Username).Should().Equal("Alice", "Bob", "Carol");
        dto.Seats.Should().NotContain(s => s.Player.Username == "<unknown>");
    }

    [Fact]
    public async Task Every_move_resolves_to_exactly_one_seat()
    {
        var (room, users) = FinishedDoudizhuRoom();
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        RoomsFixtures.SetupUserLookup(_users, users);

        var dto = await Build().Handle(new GetGameReplayQuery(room.Id), default);

        // 样本控制:走子记录里 MUST 真的用到 2 号座位,否则下面那条断言是空的 ——
        // 「每一手都解析得出来」在一个只有 0/1 的走子表上恒真。
        dto.Moves.Select(m => m.Seat).Distinct().Should().Contain(2);

        foreach (var move in dto.Moves)
        {
            dto.Seats.Should().ContainSingle(s => s.Index == move.Seat,
                $"第 {move.Ply} 手的座位号 {move.Seat} MUST 在 Seats 里解析得出人来");
        }
    }
}
