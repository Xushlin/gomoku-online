using Gomoku.Application.Features.Bots.ExecuteBotMove;
using Gomoku.Application.Features.Rooms.MakeMove;
using Gomoku.Application.Tests.Features.Rooms;
using Gomoku.Domain.Enums;
using MediatR;

namespace Gomoku.Application.Tests.Features.Bots;

public class ExecuteBotMoveCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IAiRandomProvider> _random = new();
    private readonly Mock<ISender> _sender = new();

    public ExecuteBotMoveCommandHandlerTests()
    {
        // 固定种子,使 EasyAi / MediumAi 的选点确定化。
        _random.Setup(r => r.Get()).Returns(new Random(1));
    }

    [Fact]
    public async Task When_Bots_Turn_Dispatches_MakeMoveCommand_Once()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bot = RoomsFixtures.NewBot(BotDifficulty.Easy);
        var room = RoomsFixtures.PlayingRoom(host, bot); // host=Black, bot=White
        // 当前回合 == Black(host)—— bot 不该走。先让 host 走一步,回合变成 White。
        room.PlayMove(host.Id, new Gomoku.Domain.ValueObjects.Position(7, 7), RoomsFixtures.Now.AddSeconds(2));
        room.Game!.CurrentTurn.Should().Be(Stone.White); // 确认轮到白方(bot)

        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _sender.Setup(s => s.Send(It.IsAny<MakeMoveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoveDto(2, 0, 0, Stone.White, RoomsFixtures.Now));

        var sut = new ExecuteBotMoveCommandHandler(_rooms.Object, _random.Object, _sender.Object);
        await sut.Handle(new ExecuteBotMoveCommand(bot.Id, room.Id), default);

        _sender.Verify(
            s => s.Send(
                It.Is<MakeMoveCommand>(c => c.UserId == bot.Id && c.RoomId == room.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task When_Not_Bots_Turn_Throws_NotYourTurn()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bot = RoomsFixtures.NewBot(BotDifficulty.Easy);
        var room = RoomsFixtures.PlayingRoom(host, bot); // 初始:黑方(host)回合,bot 白方
        // 没人走过,CurrentTurn == Black;bot 不该走。

        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var sut = new ExecuteBotMoveCommandHandler(_rooms.Object, _random.Object, _sender.Object);
        var act = () => sut.Handle(new ExecuteBotMoveCommand(bot.Id, room.Id), default);

        await act.Should().ThrowAsync<Gomoku.Domain.Exceptions.NotYourTurnException>();
        _sender.Verify(s => s.Send(It.IsAny<MakeMoveCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Room_Not_Found_Throws_RoomNotFound()
    {
        var bot = RoomsFixtures.NewBot(BotDifficulty.Easy);
        var missingRoomId = RoomId.NewId();
        _rooms.Setup(r => r.FindByIdAsync(missingRoomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Room?)null);

        var sut = new ExecuteBotMoveCommandHandler(_rooms.Object, _random.Object, _sender.Object);
        var act = () => sut.Handle(new ExecuteBotMoveCommand(bot.Id, missingRoomId), default);

        await act.Should().ThrowAsync<RoomNotFoundException>();
    }

    [Fact]
    public async Task Room_Not_In_Play_Throws()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bot = RoomsFixtures.NewBot(BotDifficulty.Easy);
        var room = RoomsFixtures.WaitingRoom(host); // 只有黑方,Status=Waiting

        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var sut = new ExecuteBotMoveCommandHandler(_rooms.Object, _random.Object, _sender.Object);
        var act = () => sut.Handle(new ExecuteBotMoveCommand(bot.Id, room.Id), default);

        await act.Should().ThrowAsync<Gomoku.Domain.Exceptions.RoomNotInPlayException>();
    }

    [Fact]
    public async Task Non_Player_Bot_UserId_Throws_NotAPlayer()
    {
        // 正常的真人 vs 真人对局,再给一个"孤立 bot"让它执行 → 不是玩家之一
        var black = RoomsFixtures.NewUser("Alice");
        var white = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(black, white);
        var orphanBot = RoomsFixtures.NewBot(BotDifficulty.Easy);

        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var sut = new ExecuteBotMoveCommandHandler(_rooms.Object, _random.Object, _sender.Object);
        var act = () => sut.Handle(new ExecuteBotMoveCommand(orphanBot.Id, room.Id), default);

        await act.Should().ThrowAsync<Gomoku.Domain.Exceptions.NotAPlayerException>();
    }
}
