using Gomoku.Application.Features.Rooms.Resign;
using Gomoku.Domain.Enums;
using Gomoku.Domain.Exceptions;

namespace Gomoku.Application.Tests.Features.Rooms;

public class ResignCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private ResignCommandHandler Build() => new(
        _rooms.Object, _users.Object, _clock.Object, _uow.Object, _notifier.Object, RoomsFixtures.TestGameOptions());

    [Fact]
    public async Task Success_Black_Resigns_White_Wins_Elo_Applied_Events_Fired()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(alice, bob);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddMinutes(1));
        RoomsFixtures.SetupUserLookup(_users, alice, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var ended = await Build().Handle(new ResignCommand(alice.Id, room.Id), default);

        // DTO
        ended.Result.Should().Be(GameResult.WhiteWin);
        ended.WinnerUserId.Should().Be(bob.Id.Value);
        ended.EndReason.Should().Be(GameEndReason.Resigned);
        ended.EndedAt.Should().Be(RoomsFixtures.Now.AddMinutes(1));

        // Room state
        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.Result.Should().Be(GameResult.WhiteWin);
        room.Game.EndReason.Should().Be(GameEndReason.Resigned);

        // ELO applied(双方被加载 + 战绩变动)
        alice.GamesPlayed.Should().Be(1);
        alice.Losses.Should().Be(1);
        bob.GamesPlayed.Should().Be(1);
        bob.Wins.Should().Be(1);
        bob.Rating.Should().BeGreaterThan(1200);
        alice.Rating.Should().BeLessThan(1200);

        // SaveChanges 一次
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Notifier:RoomStateChanged + GameEnded 各一次,**不**发 MoveMade
        _notifier.Verify(n => n.RoomStateChangedAsync(room.Id, It.IsAny<RoomStateDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.GameEndedAsync(room.Id,
            It.Is<GameEndedDto>(d => d.EndReason == GameEndReason.Resigned && d.Result == GameResult.WhiteWin),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.MoveMadeAsync(It.IsAny<RoomId>(), It.IsAny<MoveDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Room_Not_Found_Throws_And_Does_Nothing()
    {
        var alice = RoomsFixtures.NewUser();
        var roomId = RoomId.NewId();
        RoomsFixtures.SetupClock(_clock);
        _rooms.Setup(r => r.FindByIdAsync(roomId, It.IsAny<CancellationToken>())).ReturnsAsync((Room?)null);

        var act = () => Build().Handle(new ResignCommand(alice.Id, roomId), default);

        await act.Should().ThrowAsync<RoomNotFoundException>();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(n => n.GameEndedAsync(It.IsAny<RoomId>(), It.IsAny<GameEndedDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Non_Player_Throws_NotAPlayer_And_Does_Nothing()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var carol = RoomsFixtures.NewUser("Carol", "carol@example.com");
        var room = RoomsFixtures.PlayingRoom(alice, bob);
        RoomsFixtures.SetupClock(_clock);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var act = () => Build().Handle(new ResignCommand(carol.Id, room.Id), default);

        await act.Should().ThrowAsync<NotAPlayerException>();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _notifier.Verify(n => n.GameEndedAsync(It.IsAny<RoomId>(), It.IsAny<GameEndedDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Finished_Room_Throws_RoomNotInPlay()
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(alice, bob);
        RoomsFixtures.SetupClock(_clock);
        RoomsFixtures.SetupUserLookup(_users, alice, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        // First resign 把房间转到 Finished
        await Build().Handle(new ResignCommand(alice.Id, room.Id), default);

        // 再次认输应抛 RoomNotInPlay
        var act = () => Build().Handle(new ResignCommand(bob.Id, room.Id), default);

        await act.Should().ThrowAsync<RoomNotInPlayException>();
    }
}
