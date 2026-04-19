using Gomoku.Application.Features.Rooms.LeaveRoom;

namespace Gomoku.Application.Tests.Features.Rooms;

public class LeaveRoomCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private LeaveRoomCommandHandler Build() => new(
        _rooms.Object, _users.Object, _clock.Object, _uow.Object, _notifier.Object);

    [Fact]
    public async Task Player_Leaves_Playing_Room_Triggers_PlayerLeft()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);
        RoomsFixtures.SetupClock(_clock);
        RoomsFixtures.SetupUserLookup(_users, host, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        await Build().Handle(new LeaveRoomCommand(host.Id, room.Id), default);

        _notifier.Verify(n => n.PlayerLeftAsync(room.Id, It.Is<UserSummaryDto>(u => u.Id == host.Id.Value),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.SpectatorLeftAsync(It.IsAny<RoomId>(), It.IsAny<UserSummaryDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Spectator_Leaves_Triggers_SpectatorLeft()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var carol = RoomsFixtures.NewUser("Carol", "carol@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);
        room.JoinAsSpectator(carol.Id);

        RoomsFixtures.SetupClock(_clock);
        RoomsFixtures.SetupUserLookup(_users, host, bob, carol);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        await Build().Handle(new LeaveRoomCommand(carol.Id, room.Id), default);

        _notifier.Verify(n => n.SpectatorLeftAsync(room.Id, It.Is<UserSummaryDto>(u => u.Id == carol.Id.Value),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.PlayerLeftAsync(It.IsAny<RoomId>(), It.IsAny<UserSummaryDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
