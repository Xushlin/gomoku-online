using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Features.Rooms.JoinRoom;

namespace Gomoku.Application.Tests.Features.Rooms;

public class JoinRoomCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private JoinRoomCommandHandler Build() => new(
        _rooms.Object, _users.Object, _clock.Object, _uow.Object, _notifier.Object, RoomsFixtures.TestGameOptions());

    [Fact]
    public async Task Success_Starts_Game_And_Notifies()
    {
        var host = RoomsFixtures.NewUser("Alice", "alice@example.com");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.WaitingRoom(host);
        RoomsFixtures.SetupClock(_clock);
        RoomsFixtures.SetupUserLookup(_users, host, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var state = await Build().Handle(new JoinRoomCommand(bob.Id, room.Id), default);

        state.Status.Should().Be(RoomStatus.Playing);
        state.White!.Id.Should().Be(bob.Id.Value);

        _notifier.Verify(n => n.PlayerJoinedAsync(
            room.Id,
            It.Is<UserSummaryDto>(u => u.Id == bob.Id.Value && u.Username == "Bob"),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.RoomStateChangedAsync(
            room.Id, It.IsAny<RoomStateDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Room_Not_Found_Throws()
    {
        _rooms.Setup(r => r.FindByIdAsync(It.IsAny<RoomId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Room?)null);

        var act = () => Build().Handle(new JoinRoomCommand(UserId.NewId(), RoomId.NewId()), default);
        await act.Should().ThrowAsync<RoomNotFoundException>();
    }
}
