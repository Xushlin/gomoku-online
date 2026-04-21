using Gomoku.Application.Features.Users.GetUserGames;
using Gomoku.Application.Tests.Features.Rooms;
using Gomoku.Domain.Enums;
using Gomoku.Domain.ValueObjects;
using Move = Gomoku.Domain.ValueObjects.Move;

namespace Gomoku.Application.Tests.Features.Users.GetUserGames;

public class GetUserGamesPagedQueryHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();

    private GetUserGamesPagedQueryHandler Build() => new(_rooms.Object, _users.Object);

    private static Room MakeFinishedRoom(User alice, User bob, int movesCount = 9)
    {
        var room = Room.Create(RoomId.NewId(), "game", alice.Id, RoomsFixtures.Now);
        room.JoinAsPlayer(bob.Id, RoomsFixtures.Now.AddSeconds(1));
        // Alice 黑方连五:9 步
        var start = RoomsFixtures.Now.AddSeconds(2);
        for (var i = 0; i < 4; i++)
        {
            room.PlayMove(alice.Id, new Position(7, i), start.AddSeconds(i * 2));
            room.PlayMove(bob.Id, new Position(0, i), start.AddSeconds(i * 2 + 1));
        }
        room.PlayMove(alice.Id, new Position(7, 4), start.AddSeconds(9));
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
        first.Black.Username.Should().Be("Alice");
        first.White.Username.Should().Be("Bob");
        first.Result.Should().Be(GameResult.BlackWin);
        first.EndReason.Should().Be(GameEndReason.Connected5);
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
}
