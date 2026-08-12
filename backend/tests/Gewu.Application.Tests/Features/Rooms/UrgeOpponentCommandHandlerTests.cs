using Gomoku.Application.Features.Rooms.UrgeOpponent;
using Microsoft.Extensions.Options;

namespace Gomoku.Application.Tests.Features.Rooms;

public class UrgeOpponentCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();
    private readonly IOptions<RoomsOptions> _options = Options.Create(new RoomsOptions { UrgeCooldownSeconds = 30 });

    private UrgeOpponentCommandHandler Build() => new(
        _rooms.Object, _users.Object, _clock.Object, _uow.Object, _notifier.Object, _options);

    [Fact]
    public async Task White_Urges_Black_When_Its_Blacks_Turn()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddMinutes(1));
        RoomsFixtures.SetupUserLookup(_users, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = await Build().Handle(new UrgeOpponentCommand(bob.Id, room.Id), default);

        dto.FromUserId.Should().Be(bob.Id.Value);
        dto.FromUsername.Should().Be("Bob");

        _notifier.Verify(n => n.OpponentUrgedAsync(
            room.Id,
            It.Is<UserId>(u => u == host.Id),
            It.IsAny<UrgeDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
