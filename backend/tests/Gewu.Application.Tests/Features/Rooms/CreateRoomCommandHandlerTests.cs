using Gewu.Application.Abstractions;
using Gewu.Application.Features.Rooms.CreateRoom;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Tests.Features.Rooms;

public class CreateRoomCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IXiangqiManualRepository> _manuals = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    [Fact]
    public async Task Success_Creates_Room_And_Returns_Summary()
    {
        var host = RoomsFixtures.NewUser("Alice");
        RoomsFixtures.SetupUserLookup(_users, host);
        RoomsFixtures.SetupClock(_clock);
        _rooms.Setup(r => r.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = new CreateRoomCommandHandler(
            _rooms.Object, _users.Object, _manuals.Object, GomokuRules.Registry,
            _clock.Object, _uow.Object);
        var summary = await sut.Handle(new CreateRoomCommand(host.Id, "My Room", GameKeys.Gomoku, ManualLineId: null), default);

        summary.Name.Should().Be("My Room");
        summary.Status.Should().Be(RoomStatus.Waiting);
        summary.Host.Id.Should().Be(host.Id.Value);
        summary.Host.Username.Should().Be("Alice");
        summary.Black!.Id.Should().Be(host.Id.Value);
        summary.White.Should().BeNull();
        summary.SpectatorCount.Should().Be(0);

        _rooms.Verify(r => r.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // 不带线路 id 的建房 MUST NOT 去问古谱库 —— 与 `FakeSeeds.Calls` 那条同一个理由:
        // 一次没人用的查询,会让「这条路要不要读古谱」在读代码时得不到确定答案。
        _manuals.Verify(
            m => m.GetLineAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unknown_Host_Throws()
    {
        var missingId = UserId.NewId();
        _users.Setup(r => r.FindByIdAsync(missingId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        RoomsFixtures.SetupClock(_clock);

        var sut = new CreateRoomCommandHandler(
            _rooms.Object, _users.Object, _manuals.Object, GomokuRules.Registry,
            _clock.Object, _uow.Object);
        var act = () => sut.Handle(new CreateRoomCommand(missingId, "Name", GameKeys.Gomoku, ManualLineId: null), default);

        await act.Should().ThrowAsync<Application.Common.Exceptions.UserNotFoundException>();
    }
}
