using Gomoku.Application.Features.Rooms.MakeMove;
using Gomoku.Domain.Enums;

namespace Gomoku.Application.Tests.Features.Rooms;

public class MakeMoveCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private MakeMoveCommandHandler Build() => new(
        _rooms.Object, _users.Object, _clock.Object, _uow.Object, _notifier.Object, RoomsFixtures.TestGameOptions());

    [Fact]
    public async Task Success_Non_Winning_Move_Fires_State_And_Move_Events()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddMinutes(1));
        RoomsFixtures.SetupUserLookup(_users, host, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var move = await Build().Handle(new MakeMoveCommand(host.Id, room.Id, 7, 7), default);

        move.Ply.Should().Be(1);
        move.Stone.Should().Be(Stone.Black);
        _notifier.Verify(n => n.RoomStateChangedAsync(room.Id, It.IsAny<RoomStateDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.MoveMadeAsync(room.Id, It.IsAny<MoveDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.GameEndedAsync(It.IsAny<RoomId>(), It.IsAny<GameEndedDto>(), It.IsAny<CancellationToken>()), Times.Never);

        // 未结束局 MUST NOT 触发 ELO 计算 —— 双方 Rating / 战绩保持初始态
        host.Rating.Should().Be(1200);
        host.GamesPlayed.Should().Be(0);
        bob.Rating.Should().Be(1200);
        bob.GamesPlayed.Should().Be(0);
    }

    [Fact]
    public async Task Winning_Move_Fires_All_Three_Events_Including_GameEnded()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);

        // 预先让黑方连四,white 在边远处
        room.PlayMove(host.Id, new Gomoku.Domain.ValueObjects.Position(7, 3), RoomsFixtures.Now.AddSeconds(1));
        room.PlayMove(bob.Id, new Gomoku.Domain.ValueObjects.Position(0, 0), RoomsFixtures.Now.AddSeconds(2));
        room.PlayMove(host.Id, new Gomoku.Domain.ValueObjects.Position(7, 4), RoomsFixtures.Now.AddSeconds(3));
        room.PlayMove(bob.Id, new Gomoku.Domain.ValueObjects.Position(0, 1), RoomsFixtures.Now.AddSeconds(4));
        room.PlayMove(host.Id, new Gomoku.Domain.ValueObjects.Position(7, 5), RoomsFixtures.Now.AddSeconds(5));
        room.PlayMove(bob.Id, new Gomoku.Domain.ValueObjects.Position(0, 2), RoomsFixtures.Now.AddSeconds(6));
        room.PlayMove(host.Id, new Gomoku.Domain.ValueObjects.Position(7, 6), RoomsFixtures.Now.AddSeconds(7));
        room.PlayMove(bob.Id, new Gomoku.Domain.ValueObjects.Position(0, 3), RoomsFixtures.Now.AddSeconds(8));

        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddSeconds(9));
        RoomsFixtures.SetupUserLookup(_users, host, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // 黑方落最后一子 (7,7) 连五
        await Build().Handle(new MakeMoveCommand(host.Id, room.Id, 7, 7), default);

        room.Status.Should().Be(RoomStatus.Finished);
        _notifier.Verify(n => n.GameEndedAsync(
            room.Id,
            It.Is<GameEndedDto>(p =>
                p.Result == GameResult.BlackWin
                && p.WinnerUserId == host.Id.Value),
            It.IsAny<CancellationToken>()), Times.Once);

        // ELO 在同事务落地:两位玩家初始均为 (1200, 0),BlackWin 后
        // EloRating.Calculate(1200,0,1200,0,Win) = (1220, 1180)
        host.Rating.Should().Be(1220);
        host.GamesPlayed.Should().Be(1);
        host.Wins.Should().Be(1);
        bob.Rating.Should().Be(1180);
        bob.GamesPlayed.Should().Be(1);
        bob.Losses.Should().Be(1);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Room_Not_Found_Throws()
    {
        _rooms.Setup(r => r.FindByIdAsync(It.IsAny<RoomId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Room?)null);

        var act = () => Build().Handle(new MakeMoveCommand(UserId.NewId(), RoomId.NewId(), 0, 0), default);
        await act.Should().ThrowAsync<Application.Common.Exceptions.RoomNotFoundException>();
    }
}
