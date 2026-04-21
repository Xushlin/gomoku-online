using Gomoku.Application.Features.Rooms.TurnTimeout;
using Gomoku.Domain.Enums;
using Gomoku.Domain.Exceptions;

namespace Gomoku.Application.Tests.Features.Rooms;

public class TurnTimeoutCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private TurnTimeoutCommandHandler Build(int turnTimeoutSeconds = 60) => new(
        _rooms.Object, _users.Object, _clock.Object, _uow.Object, _notifier.Object,
        RoomsFixtures.TestGameOptions(turnTimeoutSeconds));

    [Fact]
    public async Task Timeout_Expires_Black_Turn_White_Wins_Events_Fired()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(alice, bob);
        // Room StartedAt = Now + 1s;timeout=60s → now >= StartedAt + 60s 成立
        var pastTimeout = RoomsFixtures.Now.AddSeconds(1).AddSeconds(61);
        RoomsFixtures.SetupClock(_clock, pastTimeout);
        RoomsFixtures.SetupUserLookup(_users, alice, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        await Build(60).Handle(new TurnTimeoutCommand(room.Id), default);

        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.Result.Should().Be(GameResult.WhiteWin); // Black 超时 → White 胜
        room.Game.EndReason.Should().Be(GameEndReason.TurnTimeout);

        // ELO 变动
        alice.GamesPlayed.Should().Be(1);
        alice.Losses.Should().Be(1);
        bob.GamesPlayed.Should().Be(1);
        bob.Wins.Should().Be(1);

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.GameEndedAsync(room.Id,
            It.Is<GameEndedDto>(d => d.EndReason == GameEndReason.TurnTimeout && d.Result == GameResult.WhiteWin),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Not_Yet_Timed_Out_Throws_And_Does_Nothing()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(alice, bob);
        // 仅 59s 流逝,threshold=60s
        var justShort = RoomsFixtures.Now.AddSeconds(1).AddSeconds(59);
        RoomsFixtures.SetupClock(_clock, justShort);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var act = () => Build(60).Handle(new TurnTimeoutCommand(room.Id), default);

        await act.Should().ThrowAsync<TurnNotTimedOutException>();
        room.Status.Should().Be(RoomStatus.Playing);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(n => n.GameEndedAsync(It.IsAny<RoomId>(), It.IsAny<GameEndedDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Room_Not_Found_Throws()
    {
        var roomId = RoomId.NewId();
        RoomsFixtures.SetupClock(_clock);
        _rooms.Setup(r => r.FindByIdAsync(roomId, It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);

        var act = () => Build().Handle(new TurnTimeoutCommand(roomId), default);

        await act.Should().ThrowAsync<RoomNotFoundException>();
    }

    [Fact]
    public async Task Finished_Room_Throws_RoomNotInPlay()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(alice, bob);
        room.Resign(alice.Id, RoomsFixtures.Now.AddSeconds(2));
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddMinutes(10));
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var act = () => Build().Handle(new TurnTimeoutCommand(room.Id), default);

        await act.Should().ThrowAsync<RoomNotInPlayException>();
    }
}
