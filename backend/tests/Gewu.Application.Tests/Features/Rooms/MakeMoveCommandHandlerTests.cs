using Gewu.Domain.ValueObjects;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
using Gewu.Application.Features.Rooms.MakeMove;
using Gewu.Domain.Enums;

namespace Gewu.Application.Tests.Features.Rooms;

public class MakeMoveCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private MakeMoveCommandHandler Build() => new(
        _rooms.Object, GomokuRules.Registry, _users.Object, _clock.Object, _uow.Object, _notifier.Object,
        RoomsFixtures.TestGameOptions());

    [Fact]
    public async Task Success_Non_Winning_Move_Fires_State_And_Move_Events()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddMinutes(1));
        RoomsFixtures.SetupUserLookup(_users, host, bob);
        var stats = RoomsFixtures.SetupGameStats(_users);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var move = await Build().Handle(new MakeMoveCommand(host.Id, room.Id, 7, 7), default);

        move.Ply.Should().Be(1);
        move.Stone.Should().Be(Stone.Black);
        _notifier.Verify(n => n.RoomStateChangedAsync(It.IsAny<Room>(), It.IsAny<IReadOnlyDictionary<Guid, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.MoveMadeAsync(room.Id, It.IsAny<MoveDto>(), It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.GameEndedAsync(It.IsAny<RoomId>(), It.IsAny<GameEndedDto>(), It.IsAny<CancellationToken>()), Times.Never);

        // 未结束局 MUST NOT 触发 ELO 计算,而且 MUST NOT 建战绩行 ——
        // "有行"就是"下完过这个棋种",排行榜的成员资格靠它。
        stats.Count.Should().Be(0);
    }

    [Fact]
    public async Task Winning_Move_Fires_All_Three_Events_Including_GameEnded()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob);

        // 预先让黑方连四,white 在边远处
        room.PlayMove(host.Id, MoveIntent.Place(new Gewu.Domain.ValueObjects.Position(7, 3)), RoomsFixtures.Now.AddSeconds(1), BuiltInGameRules.Gomoku);
        room.PlayMove(bob.Id, MoveIntent.Place(new Gewu.Domain.ValueObjects.Position(0, 0)), RoomsFixtures.Now.AddSeconds(2), BuiltInGameRules.Gomoku);
        room.PlayMove(host.Id, MoveIntent.Place(new Gewu.Domain.ValueObjects.Position(7, 4)), RoomsFixtures.Now.AddSeconds(3), BuiltInGameRules.Gomoku);
        room.PlayMove(bob.Id, MoveIntent.Place(new Gewu.Domain.ValueObjects.Position(0, 1)), RoomsFixtures.Now.AddSeconds(4), BuiltInGameRules.Gomoku);
        room.PlayMove(host.Id, MoveIntent.Place(new Gewu.Domain.ValueObjects.Position(7, 5)), RoomsFixtures.Now.AddSeconds(5), BuiltInGameRules.Gomoku);
        room.PlayMove(bob.Id, MoveIntent.Place(new Gewu.Domain.ValueObjects.Position(0, 2)), RoomsFixtures.Now.AddSeconds(6), BuiltInGameRules.Gomoku);
        room.PlayMove(host.Id, MoveIntent.Place(new Gewu.Domain.ValueObjects.Position(7, 6)), RoomsFixtures.Now.AddSeconds(7), BuiltInGameRules.Gomoku);
        room.PlayMove(bob.Id, MoveIntent.Place(new Gewu.Domain.ValueObjects.Position(0, 3)), RoomsFixtures.Now.AddSeconds(8), BuiltInGameRules.Gomoku);

        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddSeconds(9));
        RoomsFixtures.SetupUserLookup(_users, host, bob);
        var stats = RoomsFixtures.SetupGameStats(_users);
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

        // ELO 在同事务落地,写的是**该棋种**那一行:两位玩家首局,行由 get-or-create 建出来,
        // 初始均为 (1200, 0),BlackWin 后 EloRating.Calculate(1200,0,1200,0,Win) = (1220, 1180)
        stats.Of(host).Rating.Should().Be(1220);
        stats.Of(host).GamesPlayed.Should().Be(1);
        stats.Of(host).Wins.Should().Be(1);
        stats.Of(bob).Rating.Should().Be(1180);
        stats.Of(bob).GamesPlayed.Should().Be(1);
        stats.Of(bob).Losses.Should().Be(1);
        stats.Count.Should().Be(2, "只该建这一局棋种的两行");
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
