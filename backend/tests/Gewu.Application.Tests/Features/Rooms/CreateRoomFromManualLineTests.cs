using Gewu.Application.Abstractions;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Features.Rooms.CreateRoom;
using Gewu.Application.Features.Rooms.JoinRoom;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Xiangqi;
using Gewu.Domain.Enums;
using Gewu.Domain.Manuals;
using Gewu.Domain.ValueObjects;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// 「摆这一则古谱残局对弈」的建房路径。
/// <para>
/// 这一组的判据都围着同一件**会静默出错**的事:一条取不到的线路若被忽略,落地的是一局
/// **标准开局**的残局房 —— 而它和一局正常的棋在界面上完全一样,没有任何断言会红,
/// 除非有人正好记得自己点的是哪一则残局。
/// </para>
/// </summary>
public class CreateRoomFromManualLineTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IXiangqiManualRepository> _manuals = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private const int LineId = 42;

    /// <summary>红帅 (9,4)、红车 (9,0);黑将 (0,4)、黑卒 (3,4) —— 一则 4 子残局,**黑先走**。</summary>
    private static string EndgameBoard()
    {
        var cells = new char[XiangqiSetup.BoardLength];
        Array.Fill(cells, '.');
        cells[(0 * 9) + 4] = 'k';
        cells[(3 * 9) + 4] = 'p';
        cells[(9 * 9) + 4] = 'K';
        cells[(9 * 9) + 0] = 'R';
        return new string(cells);
    }

    private static XiangqiManualLine Line(string? board = null, int firstSeat = 1)
        => XiangqiManualLine.Create(
            "shiqingyaqu", 0, 0, "第001局 气吞关右",
            ManualVerdict.RedBetter, board ?? EndgameBoard(), firstSeat, "[]");

    private CreateRoomCommandHandler Build()
    {
        RoomsFixtures.SetupClock(_clock);
        _rooms.Setup(r => r.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()))
            .Callback<Room, CancellationToken>((r, _) => _created = r)
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return new CreateRoomCommandHandler(
            _rooms.Object, _users.Object, _manuals.Object, GomokuRules.Registry,
            _clock.Object, _uow.Object);
    }

    private Room? _created;

    private void HaveLine(XiangqiManualLine? line)
        => _manuals.Setup(m => m.GetLineAsync(LineId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(line);

    private async Task<Room> CreateAsync(int? lineId = LineId, string? gameKey = null)
    {
        var host = RoomsFixtures.NewUser("Alice");
        RoomsFixtures.SetupUserLookup(_users, host);
        await Build().Handle(
            new CreateRoomCommand(
                host.Id, "残局房", gameKey ?? GameKeys.XiangqiEndgame, lineId),
            default);
        return _created!;
    }

    // ---- 取到的是那条线路自己的局面 ----

    [Fact]
    public async Task The_room_carries_the_position_and_first_seat_from_the_line()
    {
        HaveLine(Line(firstSeat: 1));

        var room = await CreateAsync();

        room.GameKey.Should().Be(GameKeys.XiangqiEndgame);
        room.ChosenSetup.Should().NotBeNull();
        var setup = XiangqiSetup.Decode(room.ChosenSetup!, seatCount: 2);
        setup.Board.Should().Be(EndgameBoard());
        setup.FirstSeat.Should().Be(1, "1634 局里有 7 局是黑先走 —— 先手是那条线路的数据");
    }

    /// <summary>
    /// **这一条是本组的核心判据。** 从这样建出来的房间开一局,棋真的从那 4 个子开始 ——
    /// 而判据是一步**只在残局里合法**的棋。
    /// <para>
    /// 「房间上存了设置」与「对局真的从那个局面开始」是两件事:中间隔着
    /// <see cref="MatchSetup.For"/> 与 <c>Room.JoinAsPlayer</c>,而只断言前者的话,
    /// 一个把设置丢掉的实现照样全绿。
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_game_started_in_that_room_really_begins_from_that_position()
    {
        HaveLine(Line(firstSeat: 0));
        var room = await CreateAsync();

        var seeds = await JoinAsync(room);

        room.Status.Should().Be(RoomStatus.Playing);
        room.Game!.Setup.Should().Be(room.ChosenSetup);
        seeds.Calls.Should().Be(0, "选定式棋种的设置不是随机来的");

        // 红车 (9,0) → (4,0):这个 4 子残局里畅通,标准开局下 (7,0)(6,0) 有兵挡着。
        var rules = (IBoardGameRules)GomokuRules.Registry.For(GameKeys.XiangqiEndgame)!;
        var applied = rules.Apply(
            new MatchState(room.Game!.Setup, []),
            MoveIntent.Slide(new Position(9, 0), new Position(4, 0)),
            seat: 0);

        applied.Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public async Task A_black_first_line_starts_on_the_black_seat()
    {
        HaveLine(Line(firstSeat: 1));
        var room = await CreateAsync();

        await JoinAsync(room);

        room.Game!.CurrentTurn.Should().Be(1);
    }

    /// <summary>
    /// 第二个人进来时走的是**真的那条路** —— <c>JoinRoomCommandHandler</c>,而不是测试自己
    /// 拼一份设置递给 <c>Room</c>。后者测的是「我以为生产是这么接的」。
    /// </summary>
    private async Task<FakeSeeds> JoinAsync(Room room)
    {
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        RoomsFixtures.SetupUserLookup(_users, RoomsFixtures.NewUser("Alice"), bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        var seeds = new FakeSeeds();

        var handler = new JoinRoomCommandHandler(
            _rooms.Object, _users.Object, _clock.Object, _uow.Object, _notifier.Object,
            RoomsFixtures.TestGameOptions(), GomokuRules.Registry, seeds);
        await handler.Handle(new JoinRoomCommand(bob.Id, room.Id), default);
        return seeds;
    }

    // ---- 拒绝,而不是静默退回标准开局 ----

    [Fact]
    public async Task An_unknown_line_is_refused_and_no_room_is_created()
    {
        HaveLine(null);
        var host = RoomsFixtures.NewUser("Alice");
        RoomsFixtures.SetupUserLookup(_users, host);

        var act = () => Build().Handle(
            new CreateRoomCommand(host.Id, "残局房", GameKeys.XiangqiEndgame, LineId), default);

        (await act.Should().ThrowAsync<UnknownManualLineException>())
            .Which.Message.Should().Contain(LineId.ToString());

        // 拒绝**发生在造房间之前** —— 否则落地的是一局开局摆错的棋。
        _rooms.Verify(
            r => r.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// handler 自己也拦一次「键不是选定式」,而 validator 已经拦过 —— 两处**不是复制**:
    /// validator 给的是一条好看的 400,handler 给的是「房间造不出来」这条保证本身。
    /// </summary>
    [Fact]
    public async Task A_line_id_on_a_non_positional_game_is_refused_by_the_handler_too()
    {
        HaveLine(Line());
        var host = RoomsFixtures.NewUser("Alice");
        RoomsFixtures.SetupUserLookup(_users, host);

        var act = () => Build().Handle(
            new CreateRoomCommand(host.Id, "普通象棋", GameKeys.Xiangqi, LineId), default);

        await act.Should().ThrowAsync<UnknownManualLineException>();
        _rooms.Verify(
            r => r.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 库里那条线路的局面若不合法,**房间也造不出来**。
    /// <para>
    /// 播种时已经校验过一遍,所以这条守的是「库被人手改了」那一类 —— 而它的坏样子
    /// 同样是一局看着正常的棋。
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_line_whose_position_is_illegal_never_becomes_a_room()
    {
        // 黑将缺席 —— 「将死」在这样的局面上判不出来。
        var noBlackKing = EndgameBoard().Replace('k', '.');
        HaveLine(Line(board: noBlackKing));
        var host = RoomsFixtures.NewUser("Alice");
        RoomsFixtures.SetupUserLookup(_users, host);

        var act = () => Build().Handle(
            new CreateRoomCommand(host.Id, "残局房", GameKeys.XiangqiEndgame, LineId), default);

        await act.Should().ThrowAsync<Gewu.Domain.Exceptions.InvalidGameSetupException>();
        _rooms.Verify(
            r => r.AddAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- 请求里不许有盘面串 ----

    /// <summary>
    /// <see cref="CreateRoomCommand"/> 的字段**恰好**是这四个。
    /// <para>
    /// 这条断言守的是一句话:**建房请求 MUST NOT 携带盘面串**。起始局面从库里那条线路上取,
    /// 而让客户端递盘面等于让客户端定义棋局。
    /// </para>
    /// <para>
    /// 写成「恰好」而不是「不含某个名字」,因为要拦的不是 <c>StartPosition</c> 这个名字 ——
    /// 一个叫 <c>Position</c>、<c>Fen</c>、<c>Board</c> 的字段一样是那个口子。加字段时这条会红,
    /// 而那正是该问「这个东西能不能装下一盘棋」的时刻。
    /// </para>
    /// <para>
    /// 现有四个字段里没有一个装得下:<c>ManualLineId</c> 是 <c>int?</c>,<c>GameKey</c> 要能在
    /// 注册表里解析出来,<c>Name</c> 上限 50 字符而盘面串是 90。
    /// </para>
    /// </summary>
    [Fact]
    public void The_create_room_command_carries_an_id_and_never_a_board()
    {
        var properties = typeof(CreateRoomCommand)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract")
            .Select(p => p.Name);

        properties.Should().BeEquivalentTo(
            [
                nameof(CreateRoomCommand.HostUserId),
                nameof(CreateRoomCommand.Name),
                nameof(CreateRoomCommand.GameKey),
                nameof(CreateRoomCommand.ManualLineId),
            ],
            "建房请求里不许出现一个装得下盘面的字段");

        typeof(CreateRoomCommand).GetProperty(nameof(CreateRoomCommand.ManualLineId))!
            .PropertyType.Should().Be(typeof(int?), "一个 int? 递不出一盘棋 —— 那正是它被选中的形状");
    }
}
