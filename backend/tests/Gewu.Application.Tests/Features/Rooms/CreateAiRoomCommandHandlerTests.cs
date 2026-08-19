using Gewu.Application.Features.Rooms.CreateAiRoom;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Tests.Features.Rooms;

public class CreateAiRoomCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    [Fact]
    public async Task Success_Creates_AI_Room_In_Playing_State()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bot = RoomsFixtures.NewBot(BotDifficulty.Medium);

        RoomsFixtures.SetupUserLookup(_users, host);
        _users.Setup(u => u.FindBotByDifficultyAsync(BotDifficulty.Medium, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bot);
        RoomsFixtures.SetupClock(_clock);
        _rooms.Setup(r => r.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = new CreateAiRoomCommandHandler(_rooms.Object, _users.Object, _clock.Object, _uow.Object, RoomsFixtures.TestGameOptions(), GomokuRules.Registry, new FakeSeeds());
        var state = await sut.Handle(
            new CreateAiRoomCommand(host.Id, "AI match", BotDifficulty.Medium, Stone.Black, GameKeys.Gomoku),
            default);

        state.Name.Should().Be("AI match");
        state.Status.Should().Be(RoomStatus.Playing);
        state.Host.Id.Should().Be(host.Id.Value);
        state.Black!.Id.Should().Be(host.Id.Value);
        state.White!.Id.Should().Be(bot.Id.Value);
        state.White.Username.Should().Be("AI_Medium");
        state.Game.Should().NotBeNull();
        state.Game!.CurrentSeat.Should().Be(0, "先手座位号是 0 —— 颜色是显示层的事");
        state.Game.Moves.Should().BeEmpty();

        _rooms.Verify(r => r.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unknown_Host_Throws_UserNotFound()
    {
        var missingId = UserId.NewId();
        _users.Setup(u => u.FindByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        RoomsFixtures.SetupClock(_clock);

        var sut = new CreateAiRoomCommandHandler(_rooms.Object, _users.Object, _clock.Object, _uow.Object, RoomsFixtures.TestGameOptions(), GomokuRules.Registry, new FakeSeeds());
        var act = () => sut.Handle(new CreateAiRoomCommand(missingId, "AI", BotDifficulty.Easy, Stone.Black, GameKeys.Gomoku), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Missing_Bot_Seed_Throws_UserNotFound()
    {
        var host = RoomsFixtures.NewUser();
        RoomsFixtures.SetupUserLookup(_users, host);
        _users.Setup(u => u.FindBotByDifficultyAsync(BotDifficulty.Easy, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        RoomsFixtures.SetupClock(_clock);

        var sut = new CreateAiRoomCommandHandler(_rooms.Object, _users.Object, _clock.Object, _uow.Object, RoomsFixtures.TestGameOptions(), GomokuRules.Registry, new FakeSeeds());
        var act = () => sut.Handle(new CreateAiRoomCommand(host.Id, "AI", BotDifficulty.Easy, Stone.Black, GameKeys.Gomoku), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task CreateAiRoom_Hard_Difficulty_Succeeds()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bot = RoomsFixtures.NewBot(BotDifficulty.Hard);

        RoomsFixtures.SetupUserLookup(_users, host);
        _users.Setup(u => u.FindBotByDifficultyAsync(BotDifficulty.Hard, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bot);
        RoomsFixtures.SetupClock(_clock);
        _rooms.Setup(r => r.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = new CreateAiRoomCommandHandler(_rooms.Object, _users.Object, _clock.Object, _uow.Object, RoomsFixtures.TestGameOptions(), GomokuRules.Registry, new FakeSeeds());
        var state = await sut.Handle(
            new CreateAiRoomCommand(host.Id, "AI Hard match", BotDifficulty.Hard, Stone.Black, GameKeys.Gomoku),
            default);

        state.Status.Should().Be(RoomStatus.Playing);
        state.White!.Id.Should().Be(bot.Id.Value);
        state.White.Username.Should().Be("AI_Hard");
    }

    [Fact]
    public async Task Human_White_Swaps_Seats_So_Bot_Plays_Black()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bot = RoomsFixtures.NewBot(BotDifficulty.Medium);

        RoomsFixtures.SetupUserLookup(_users, host);
        _users.Setup(u => u.FindBotByDifficultyAsync(BotDifficulty.Medium, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bot);
        RoomsFixtures.SetupClock(_clock);
        _rooms.Setup(r => r.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = new CreateAiRoomCommandHandler(_rooms.Object, _users.Object, _clock.Object, _uow.Object, RoomsFixtures.TestGameOptions(), GomokuRules.Registry, new FakeSeeds());
        var state = await sut.Handle(
            new CreateAiRoomCommand(host.Id, "Defense", BotDifficulty.Medium, Stone.White, GameKeys.Gomoku),
            default);

        // Seats swapped: bot on Black (plays first), human on White, host
        // still the human.
        state.Black!.Id.Should().Be(bot.Id.Value);
        state.White!.Id.Should().Be(host.Id.Value);
        state.Host.Id.Should().Be(host.Id.Value);
        // 先手座位不变 —— 仍然是 0 号，而现在坐在 0 号上的是机器人。
        state.Game!.CurrentSeat.Should().Be(0);
        state.Game.Moves.Should().BeEmpty();
    }

    [Fact]
    public async Task Bot_Host_Is_Rejected_As_ValidationException()
    {
        var botHost = RoomsFixtures.NewBot(BotDifficulty.Easy);
        RoomsFixtures.SetupUserLookup(_users, botHost);
        RoomsFixtures.SetupClock(_clock);

        var sut = new CreateAiRoomCommandHandler(_rooms.Object, _users.Object, _clock.Object, _uow.Object, RoomsFixtures.TestGameOptions(), GomokuRules.Registry, new FakeSeeds());
        var act = () => sut.Handle(
            new CreateAiRoomCommand(botHost.Id, "AI", BotDifficulty.Easy, Stone.Black, GameKeys.Gomoku),
            default);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
