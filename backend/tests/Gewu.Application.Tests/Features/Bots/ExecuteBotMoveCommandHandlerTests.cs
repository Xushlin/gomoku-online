using Gewu.Domain.ValueObjects;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
using Gewu.Application.Features.Bots.ExecuteBotMove;
using Gewu.Application.Features.Rooms.MakeMove;
using Gewu.Application.Tests.Features.Rooms;
using Gewu.Domain.Enums;
using MediatR;

namespace Gewu.Application.Tests.Features.Bots;

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
        room.PlayMove(host.Id, MoveIntent.Place(new Gewu.Domain.ValueObjects.Position(7, 7)), RoomsFixtures.Now.AddSeconds(2), BuiltInGameRules.Gomoku);
        room.Game!.CurrentTurn.Should().Be(Room.SecondSeat); // 确认轮到后手座位(bot)

        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _sender.Setup(s => s.Send(It.IsAny<MakeMoveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoveDto(2, 0, 0, 1, RoomsFixtures.Now));

        var sut = new ExecuteBotMoveCommandHandler(_rooms.Object, GomokuRules.Registry, GomokuRules.AiRegistry, _random.Object, _sender.Object);
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

        var sut = new ExecuteBotMoveCommandHandler(_rooms.Object, GomokuRules.Registry, GomokuRules.AiRegistry, _random.Object, _sender.Object);
        var act = () => sut.Handle(new ExecuteBotMoveCommand(bot.Id, room.Id), default);

        await act.Should().ThrowAsync<Gewu.Domain.Exceptions.NotYourTurnException>();
        _sender.Verify(s => s.Send(It.IsAny<MakeMoveCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Room_Not_Found_Throws_RoomNotFound()
    {
        var bot = RoomsFixtures.NewBot(BotDifficulty.Easy);
        var missingRoomId = RoomId.NewId();
        _rooms.Setup(r => r.FindByIdAsync(missingRoomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Room?)null);

        var sut = new ExecuteBotMoveCommandHandler(_rooms.Object, GomokuRules.Registry, GomokuRules.AiRegistry, _random.Object, _sender.Object);
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

        var sut = new ExecuteBotMoveCommandHandler(_rooms.Object, GomokuRules.Registry, GomokuRules.AiRegistry, _random.Object, _sender.Object);
        var act = () => sut.Handle(new ExecuteBotMoveCommand(bot.Id, room.Id), default);

        await act.Should().ThrowAsync<Gewu.Domain.Exceptions.RoomNotInPlayException>();
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

        var sut = new ExecuteBotMoveCommandHandler(_rooms.Object, GomokuRules.Registry, GomokuRules.AiRegistry, _random.Object, _sender.Object);
        var act = () => sut.Handle(new ExecuteBotMoveCommand(orphanBot.Id, room.Id), default);

        await act.Should().ThrowAsync<Gewu.Domain.Exceptions.NotAPlayerException>();
    }

    [Fact]
    public async Task Plays_A_TicTacToe_Room_With_The_TicTacToe_AI()
    {
        // 同一个 bot 账号,不同棋种 —— 走哪套算法由 (GameKey, Difficulty) 经注册表解析。
        // 这是"bot 账号是身份而不是策略"那句话的可执行版本。
        var host = RoomsFixtures.NewUser("Alice");
        var bot = RoomsFixtures.NewBot(BotDifficulty.Hard);
        var room = RoomsFixtures.PlayingRoom(host, bot, "ttt", GameKeys.TicTacToe);
        room.PlayMove(
            host.Id,
            MoveIntent.Place(new Gewu.Domain.ValueObjects.Position(0, 0)),
            RoomsFixtures.Now.AddSeconds(2),
            BuiltInGameRules.TicTacToe);

        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _sender.Setup(s => s.Send(It.IsAny<MakeMoveCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MoveDto(2, 1, 1, 1, RoomsFixtures.Now));

        var sut = new ExecuteBotMoveCommandHandler(
            _rooms.Object, GomokuRules.Registry, GomokuRules.AiRegistry, _random.Object, _sender.Object);
        await sut.Handle(new ExecuteBotMoveCommand(bot.Id, room.Id), default);

        // 落点必须在 3×3 界内 —— 若它拿到了五子棋的 AI,选点会落在 15×15 的某处。
        _sender.Verify(
            s => s.Send(
                It.Is<MakeMoveCommand>(c => c.Row >= 0 && c.Row < 3 && c.Col >= 0 && c.Col < 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Game_Without_An_AI_Is_A_404_Not_A_Crash()
    {
        // 规则解析得出、AI 解析不出 —— 一个只支持人人对战的棋种。两条解析路径
        // 都可能失败,都映射成同一个 404,都 MUST NOT 变成未处理异常。
        var host = RoomsFixtures.NewUser("Alice");
        var bot = RoomsFixtures.NewBot(BotDifficulty.Easy);
        var room = RoomsFixtures.PlayingRoom(host, bot, "ttt", GameKeys.TicTacToe);
        room.PlayMove(
            host.Id,
            MoveIntent.Place(new Gewu.Domain.ValueObjects.Position(0, 0)),
            RoomsFixtures.Now.AddSeconds(2),
            BuiltInGameRules.TicTacToe);

        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var sut = new ExecuteBotMoveCommandHandler(
            _rooms.Object, GomokuRules.Registry, GomokuRules.GomokuAiOnly, _random.Object, _sender.Object);
        var act = () => sut.Handle(new ExecuteBotMoveCommand(bot.Id, room.Id), default);

        await act.Should().ThrowAsync<Gewu.Application.Common.Exceptions.RoomNotFoundException>();
        _sender.Verify(
            s => s.Send(It.IsAny<MakeMoveCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
