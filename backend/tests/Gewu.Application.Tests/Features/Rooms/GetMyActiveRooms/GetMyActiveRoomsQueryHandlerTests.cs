using Gewu.Application.Features.Rooms.GetMyActiveRooms;
using Gewu.Application.Tests.Features.Rooms;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Tests.Features.Rooms.GetMyActiveRooms;

public class GetMyActiveRoomsQueryHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private GetMyActiveRoomsQueryHandler Build() => new(_rooms.Object, _users.Object);

    [Fact]
    public async Task Success_Maps_Rooms_To_Summaries()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var waiting = RoomsFixtures.WaitingRoom(alice, "Wait Room");
        var playing = RoomsFixtures.PlayingRoom(alice, bob, "Play Room");
        _rooms.Setup(r => r.GetActiveRoomsByUserAsync(alice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Room>)new[] { playing, waiting });
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var result = await Build().Handle(new GetMyActiveRoomsQuery(alice.Id), default);

        result.Should().HaveCount(2);
        result[0].Host.Username.Should().Be("Alice");
        result[0].Status.Should().Be(RoomStatus.Playing);
        result[1].Status.Should().Be(RoomStatus.Waiting);
        // 相关 usernames 都填上
        result[0].White!.Username.Should().Be("Bob");
    }

    [Fact]
    public async Task My_Active_Rooms_Span_Games_On_Purpose()
    {
        // 这条测试钉住的是一个**刻意的**不一致:大厅按棋种过滤,"我的对局"不过滤。
        // 将来有人为了"和大厅一致"给这个查询加棋种参数时,应该先被这条测试拦住,
        // 再去读 GetMyActiveRoomsQuery 的文档注释 —— 那里写着为什么。
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var gomoku = RoomsFixtures.PlayingRoom(alice, bob, "Gomoku game", GameKeys.Gomoku);
        var ttt = RoomsFixtures.PlayingRoom(alice, bob, "TicTacToe game", GameKeys.TicTacToe);
        _rooms.Setup(r => r.GetActiveRoomsByUserAsync(alice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Room>)new[] { gomoku, ttt });
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var result = await Build().Handle(new GetMyActiveRoomsQuery(alice.Id), default);

        result.Should().HaveCount(2);
        result.Select(r => r.Name).Should().BeEquivalentTo("Gomoku game", "TicTacToe game");
    }

    [Fact]
    public async Task Empty_Repo_Returns_Empty_List_No_Username_Lookup()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        _rooms.Setup(r => r.GetActiveRoomsByUserAsync(alice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Room>)Array.Empty<Room>());

        var result = await Build().Handle(new GetMyActiveRoomsQuery(alice.Id), default);

        result.Should().BeEmpty();
        _users.Verify(u => u.FindByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Alice_As_White_Is_Included()
    {
        // Bob 是 Host + Black,Alice 是 White(通过 JoinAsPlayer)
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var alice = RoomsFixtures.NewUser("Alice");
        var room = RoomsFixtures.PlayingRoom(bob, alice, "Bob's room");
        _rooms.Setup(r => r.GetActiveRoomsByUserAsync(alice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Room>)new[] { room });
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var result = await Build().Handle(new GetMyActiveRoomsQuery(alice.Id), default);

        result.Should().HaveCount(1);
        result[0].Host.Username.Should().Be("Bob");
        result[0].Black!.Username.Should().Be("Bob");
        result[0].White!.Username.Should().Be("Alice");
    }

    [Fact]
    public async Task Usernames_Lookup_Called_Once_With_Distinct_Ids()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        // 两个房间,都有 Alice+Bob;distinct 后只查 2 个 id
        var r1 = RoomsFixtures.PlayingRoom(alice, bob);
        var r2 = RoomsFixtures.PlayingRoom(alice, bob);
        _rooms.Setup(r => r.GetActiveRoomsByUserAsync(alice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Room>)new[] { r1, r2 });
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var result = await Build().Handle(new GetMyActiveRoomsQuery(alice.Id), default);

        result.Should().HaveCount(2);
        // 每个 UserId 只被 lookup 一次
        _users.Verify(u => u.FindByIdAsync(alice.Id, It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.FindByIdAsync(bob.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
