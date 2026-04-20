using Gomoku.Application.Features.Rooms.Dissolve;
using Gomoku.Domain.Exceptions;

namespace Gomoku.Application.Tests.Features.Rooms;

public class DissolveRoomCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private DissolveRoomCommandHandler Build() => new(_rooms.Object, _uow.Object, _notifier.Object);

    [Fact]
    public async Task Host_Dissolves_Waiting_Room_Successfully()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var room = RoomsFixtures.WaitingRoom(host);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        await Build().Handle(new DissolveRoomCommand(host.Id, room.Id), default);

        _rooms.Verify(r => r.DeleteAsync(room, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.RoomDissolvedAsync(room.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Room_Not_Found_Throws_RoomNotFound()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var roomId = RoomId.NewId();
        _rooms.Setup(r => r.FindByIdAsync(roomId, It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);

        var act = () => Build().Handle(new DissolveRoomCommand(host.Id, roomId), default);

        await act.Should().ThrowAsync<Gomoku.Application.Common.Exceptions.RoomNotFoundException>();
        _rooms.Verify(r => r.DeleteAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(n => n.RoomDissolvedAsync(It.IsAny<RoomId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Non_Host_Throws_NotRoomHost_And_Does_Nothing()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var stranger = RoomsFixtures.NewUser("Eve", "eve@example.com");
        var room = RoomsFixtures.WaitingRoom(host);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var act = () => Build().Handle(new DissolveRoomCommand(stranger.Id, room.Id), default);

        await act.Should().ThrowAsync<NotRoomHostException>();
        _rooms.Verify(r => r.DeleteAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(n => n.RoomDissolvedAsync(It.IsAny<RoomId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Playing_Room_Throws_RoomNotWaiting_And_Does_Nothing()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var act = () => Build().Handle(new DissolveRoomCommand(host.Id, room.Id), default);

        await act.Should().ThrowAsync<RoomNotWaitingException>();
        _rooms.Verify(r => r.DeleteAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(n => n.RoomDissolvedAsync(It.IsAny<RoomId>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
