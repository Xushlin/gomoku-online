using Gomoku.Application.Features.Rooms.CreateAiRoom;

namespace Gomoku.Application.Tests.Features.Rooms;

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

        var sut = new CreateAiRoomCommandHandler(_rooms.Object, _users.Object, _clock.Object, _uow.Object, RoomsFixtures.TestGameOptions());
        var state = await sut.Handle(
            new CreateAiRoomCommand(host.Id, "AI match", BotDifficulty.Medium),
            default);

        state.Name.Should().Be("AI match");
        state.Status.Should().Be(RoomStatus.Playing);
        state.Host.Id.Should().Be(host.Id.Value);
        state.Black!.Id.Should().Be(host.Id.Value);
        state.White!.Id.Should().Be(bot.Id.Value);
        state.White.Username.Should().Be("AI_Medium");
        state.Game.Should().NotBeNull();
        state.Game!.CurrentTurn.Should().Be(Gomoku.Domain.Enums.Stone.Black);
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

        var sut = new CreateAiRoomCommandHandler(_rooms.Object, _users.Object, _clock.Object, _uow.Object, RoomsFixtures.TestGameOptions());
        var act = () => sut.Handle(new CreateAiRoomCommand(missingId, "AI", BotDifficulty.Easy), default);

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

        var sut = new CreateAiRoomCommandHandler(_rooms.Object, _users.Object, _clock.Object, _uow.Object, RoomsFixtures.TestGameOptions());
        var act = () => sut.Handle(new CreateAiRoomCommand(host.Id, "AI", BotDifficulty.Easy), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Bot_Host_Is_Rejected_As_ValidationException()
    {
        var botHost = RoomsFixtures.NewBot(BotDifficulty.Easy);
        RoomsFixtures.SetupUserLookup(_users, botHost);
        RoomsFixtures.SetupClock(_clock);

        var sut = new CreateAiRoomCommandHandler(_rooms.Object, _users.Object, _clock.Object, _uow.Object, RoomsFixtures.TestGameOptions());
        var act = () => sut.Handle(
            new CreateAiRoomCommand(botHost.Id, "AI", BotDifficulty.Easy),
            default);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
