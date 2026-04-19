using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Features.Rooms.GetRoomList;
using Gomoku.Application.Features.Rooms.GetRoomState;

namespace Gomoku.Application.Tests.Features.Rooms;

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

        _rooms.Setup(r => r.GetActiveRoomsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Room> { r1, r2 });
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var sut = new GetRoomListQueryHandler(_rooms.Object, _users.Object);
        var result = await sut.Handle(new GetRoomListQuery(), default);

        result.Should().HaveCount(2);
        result.Select(r => r.Status).Should().BeEquivalentTo(new[] { RoomStatus.Waiting, RoomStatus.Playing });
    }

    [Fact]
    public async Task GetRoomState_Success()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(alice, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        RoomsFixtures.SetupUserLookup(_users, alice, bob);

        var sut = new GetRoomStateQueryHandler(_rooms.Object, _users.Object);
        var dto = await sut.Handle(new GetRoomStateQuery(room.Id), default);

        dto.Status.Should().Be(RoomStatus.Playing);
        dto.Game.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRoomState_Not_Found_Throws()
    {
        _rooms.Setup(r => r.FindByIdAsync(It.IsAny<RoomId>(), It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);

        var sut = new GetRoomStateQueryHandler(_rooms.Object, _users.Object);
        var act = () => sut.Handle(new GetRoomStateQuery(RoomId.NewId()), default);
        await act.Should().ThrowAsync<RoomNotFoundException>();
    }
}
