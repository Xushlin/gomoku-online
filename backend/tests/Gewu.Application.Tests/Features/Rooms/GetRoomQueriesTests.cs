using Gewu.Application.Common.Exceptions;
using Gewu.Application.Features.Rooms.GetRoomList;
using Gewu.Application.Features.Rooms.GetRoomState;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Tests.Features.Rooms;

public class GetRoomQueriesTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();

    [Fact]
    public async Task GetRoomList_Returns_Summaries()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var r1 = RoomsFixtures.WaitingRoom(alice, "Room A");
        var r2 = RoomsFixtures.PlayingRoom(alice, bob, "Room B");

        _rooms.Setup(r => r.GetActiveRoomsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Room> { r1, r2 });
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var sut = new GetRoomListQueryHandler(_rooms.Object, _users.Object);
        var result = await sut.Handle(new GetRoomListQuery(GameKeys.Gomoku), default);

        result.Should().HaveCount(2);
        result.Select(r => r.Status).Should().BeEquivalentTo(new[] { RoomStatus.Waiting, RoomStatus.Playing });
    }

    [Theory]
    [InlineData(GameKeys.Gomoku)]
    [InlineData(GameKeys.TicTacToe)]
    [InlineData("xiangqi")]
    public async Task GetRoomList_Passes_The_Game_Key_Down_To_The_Repository(string gameKey)
    {
        // 过滤本身是 EF 谓词,只能在 Infrastructure 层对着真库测(见 RoomRepository 的测试)。
        // 这里能证明也只该证明的是:handler 没有把棋种吞掉,而是把它交给了仓库 ——
        // 若它在内存里过滤,或干脆忽略这个字段,这条断言会挂。
        var seen = new List<string>();
        _rooms.Setup(r => r.GetActiveRoomsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string key, CancellationToken _) => seen.Add(key))
            .ReturnsAsync(new List<Room>());

        var sut = new GetRoomListQueryHandler(_rooms.Object, _users.Object);
        var result = await sut.Handle(new GetRoomListQuery(gameKey), default);

        seen.Should().Equal(gameKey);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoomList_Of_An_Unknown_Game_Is_Empty_Not_An_Error()
    {
        _rooms.Setup(r => r.GetActiveRoomsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Room>());

        var sut = new GetRoomListQueryHandler(_rooms.Object, _users.Object);
        var act = () => sut.Handle(new GetRoomListQuery("a-game-nobody-registered"), default);

        (await act.Should().NotThrowAsync()).Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoomState_Success()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(alice, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var sut = new GetRoomStateQueryHandler(_rooms.Object, _users.Object, RoomsFixtures.TestGameOptions());
        var dto = await sut.Handle(new GetRoomStateQuery(room.Id), default);

        dto.Status.Should().Be(RoomStatus.Playing);
        dto.Game.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRoomState_Not_Found_Throws()
    {
        _rooms.Setup(r => r.FindByIdAsync(It.IsAny<RoomId>(), It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);

        var sut = new GetRoomStateQueryHandler(_rooms.Object, _users.Object, RoomsFixtures.TestGameOptions());
        var act = () => sut.Handle(new GetRoomStateQuery(RoomId.NewId()), default);
        await act.Should().ThrowAsync<RoomNotFoundException>();
    }
}
