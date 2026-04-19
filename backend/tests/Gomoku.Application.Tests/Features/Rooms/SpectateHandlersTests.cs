using Gomoku.Application.Features.Rooms.JoinAsSpectator;
using Gomoku.Application.Features.Rooms.LeaveAsSpectator;

namespace Gomoku.Application.Tests.Features.Rooms;

public class SpectateHandlersTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    [Fact]
    public async Task Join_As_Spectator_Broadcasts()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var carol = RoomsFixtures.NewUser("Carol", "carol@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);
        RoomsFixtures.SetupUserLookup(_users, host, bob, carol);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var sut = new JoinAsSpectatorCommandHandler(_rooms.Object, _users.Object, _uow.Object, _notifier.Object);
        await sut.Handle(new JoinAsSpectatorCommand(carol.Id, room.Id), default);

        room.Spectators.Should().Contain(carol.Id);
        _notifier.Verify(n => n.SpectatorJoinedAsync(room.Id,
            It.Is<UserSummaryDto>(u => u.Id == carol.Id.Value),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.RoomStateChangedAsync(room.Id,
            It.IsAny<RoomStateDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Leave_As_Spectator_Broadcasts_SpectatorLeft()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var carol = RoomsFixtures.NewUser("Carol", "carol@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);
        room.JoinAsSpectator(carol.Id);

        RoomsFixtures.SetupUserLookup(_users, host, bob, carol);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var sut = new LeaveAsSpectatorCommandHandler(_rooms.Object, _users.Object, _uow.Object, _notifier.Object);
        await sut.Handle(new LeaveAsSpectatorCommand(carol.Id, room.Id), default);

        room.Spectators.Should().NotContain(carol.Id);
        _notifier.Verify(n => n.SpectatorLeftAsync(room.Id,
            It.Is<UserSummaryDto>(u => u.Id == carol.Id.Value),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
