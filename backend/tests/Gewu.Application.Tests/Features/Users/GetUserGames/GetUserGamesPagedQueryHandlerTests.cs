using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
using Gewu.Application.Features.Users.GetUserGames;
using Gewu.Application.Tests.Features.Rooms;
using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;
using Move = Gewu.Domain.ValueObjects.Move;

namespace Gewu.Application.Tests.Features.Users.GetUserGames;

public class GetUserGamesPagedQueryHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();

    private GetUserGamesPagedQueryHandler Build() => new(_rooms.Object, _users.Object);

    private static Room MakeFinishedRoom(User alice, User bob, int movesCount = 9)
    {
        var room = Room.Create(RoomId.NewId(), "game", alice.Id, RoomsFixtures.Now, GameKeys.Gomoku);
        room.JoinAsPlayer(bob.Id, RoomsFixtures.Now.AddSeconds(1), BuiltInGameRules.Gomoku, setup: null);
        // Alice 黑方连五:9 步
        var start = RoomsFixtures.Now.AddSeconds(2);
        for (var i = 0; i < 4; i++)
        {
            room.PlayMove(alice.Id, MoveIntent.Place(new Position(7, i)), start.AddSeconds(i * 2), BuiltInGameRules.Gomoku);
            room.PlayMove(bob.Id, MoveIntent.Place(new Position(0, i)), start.AddSeconds(i * 2 + 1), BuiltInGameRules.Gomoku);
        }
        room.PlayMove(alice.Id, MoveIntent.Place(new Position(7, 4)), start.AddSeconds(9), BuiltInGameRules.Gomoku);
        return room;
    }

    [Fact]
    public async Task Success_Maps_All_Rooms_Into_Summary_With_Usernames()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var r1 = MakeFinishedRoom(alice, bob);
        var r2 = MakeFinishedRoom(alice, bob);
        var r3 = MakeFinishedRoom(alice, bob);
        _rooms.Setup(r => r.GetUserFinishedGamesPagedAsync(
                alice.Id, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Room>)new[] { r1, r2, r3 }, 3));
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var result = await Build().Handle(
            new GetUserGamesPagedQuery(alice.Id, 1, 20), default);

        result.Total.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.Items.Should().HaveCount(3);

        var first = result.Items[0];
        first.RoomId.Should().Be(r1.Id.Value);
        first.Seats.Should().HaveCount(2);
        first.Seats.Select(s => s.Index).Should().Equal(0, 1);
        first.Seats.Select(s => s.Player.Username).Should().Equal("Alice", "Bob");
        first.Result.Should().Be(GameResult.Decided);
        first.EndReason.Should().Be(GameEndReason.Decided);
        first.MoveCount.Should().Be(9);
    }

    [Fact]
    public async Task Empty_Result_Returns_Empty_List_With_Zero_Total()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        _rooms.Setup(r => r.GetUserFinishedGamesPagedAsync(
                alice.Id, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Room>)Array.Empty<Room>(), 0));

        var result = await Build().Handle(
            new GetUserGamesPagedQuery(alice.Id, 1, 20), default);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Out_Of_Range_Page_Returns_Empty_But_Keeps_Total()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        // Repo 对越界 page 返回空 rooms,但 Total 仍反映总数(如 total=5,page=10)
        _rooms.Setup(r => r.GetUserFinishedGamesPagedAsync(
                alice.Id, 10, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Room>)Array.Empty<Room>(), 5));

        var result = await Build().Handle(
            new GetUserGamesPagedQuery(alice.Id, 10, 2), default);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(5);
        result.Page.Should().Be(10);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task MoveCount_Reflects_Moves_Count()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = MakeFinishedRoom(alice, bob, movesCount: 9);
        _rooms.Setup(r => r.GetUserFinishedGamesPagedAsync(
                alice.Id, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<Room>)new[] { room }, 1));
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var result = await Build().Handle(
            new GetUserGamesPagedQuery(alice.Id, 1, 20), default);

        result.Items[0].MoveCount.Should().Be(9);
    }

    [Fact]
    public async Task A_three_seat_game_in_the_history_names_all_three()
    {
        // 修之前:handler 与回放那份逐字同形,无条件读 BlackPlayerId / WhitePlayerId,
        // 于是 2 号座位上的人不进战绩 —— 而仓储**不按棋种过滤**,三座位对局照样进来。
        var (ddz, players) = RoomsFixtures.FinishedDoudizhuRoom();
        var alice = players[0];
        var bob = players[1];
        var gomoku = MakeFinishedRoom(alice, bob);

        // **同一份响应里两种形状都在。** 只有三座位的样本会让「两座位仍是两条」无从检验;
        // 只有两座位的样本会让「每个座位都在」恒真 —— 那正是这个缺陷活到今天的原因。
        _rooms.Setup(r => r.GetUserFinishedGamesPagedAsync(
                alice.Id, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Room> { ddz, gomoku }, 2));
        RoomsFixtures.SetupUserLookup(_users, players);

        var result = await Build().Handle(new GetUserGamesPagedQuery(alice.Id, 1, 10), default);

        var threeSeat = result.Items.Single(i => i.Seats.Count == 3);
        threeSeat.Seats.Select(s => s.Index).Should().Equal(0, 1, 2);
        threeSeat.Seats.Select(s => s.Player.Username).Should().Equal("Alice", "Bob", "Carol");
        threeSeat.Seats.Should().NotContain(s => s.Player.Username == "<unknown>");

        // 反面控制:两座位那条**恰好**两条,第四个座位没被凭空补出来。
        result.Items.Single(i => i.Seats.Count == 2)
            .Seats.Select(s => s.Index).Should().Equal(0, 1);
    }
}
