using Gewu.Application.Features.Rooms.GetGameReplay;
using Gewu.Application.Features.Rooms.MakeMove;
using Gewu.Application.Features.Rooms.Resign;
using Gewu.Application.Features.Rooms.TurnTimeout;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.ValueObjects;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// 不计分棋种(<c>IGameRules.IsRated == false</c>)在**每一条**对局结束路径上都不动评分。
/// <para>
/// 一局棋有三条结束方式:落子成胜负 / 认输 / 超时判负。每一条都单独调一次
/// <c>GameEloApplier</c>,所以每一条都要单独验 —— "只有认输那条漏了"这种 bug
/// 在使用中几乎不可能被注意到,只会表现为排行榜慢慢变得不对。
/// </para>
/// <para>
/// 同样要验的是**对局本身照常结束**:不计分不该削弱对局记录。Status 进 Finished、
/// EndReason 写入、GameEnded 照常广播。一局棋是否算分,不影响它是否是一局棋。
/// </para>
/// <para>
/// **这段注释此前是错的。** 原文写着"这组测试是限期的 —— <c>add-per-game-rating</c> 让每个
/// 棋种各算各的之后,<c>IsRated</c> 连同本文件一起删除"。两处都错:
/// </para>
/// <para>
/// 其一,本文件不会被删。"不计分棋种在每一条结束路径上都不动评分"这条行为在 per-game
/// 评分之后依然存在 —— 只是那时"不计分"的原因从"怕污染共享池"变成"本棋种没有人类对手池"。
/// 其二,拆除条件不是那个变更:一字棋没有人人对战,唯一的对手是机器人,而机器人对局是计分的,
/// 所以它的阶梯排出来的是"谁刷 Easy 档刷得多"。池子分开解决不了这件事。
/// </para>
/// <para>
/// 现在 <c>IsRated</c> 受不变量约束(<c>IsRated ⇒ SupportsHumanVsHuman</c>,由
/// <c>NInARowRules</c> 构造器与一条遍历注册表的测试双重强制),所以一字棋的"不计分"不再是
/// 一个需要有人记得回来翻的判断 —— 见 <c>add-game-capabilities</c>。
/// </para>
/// </summary>
public class UnratedGameEloTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private static readonly IGameRules TicTacToe = BuiltInGameRules.TicTacToe;

    private (User Host, User Guest, Room Room) TicTacToeRoom()
    {
        var host = RoomsFixtures.NewUser("Alice");
        var guest = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, guest, "ttt", GameKeys.TicTacToe);

        RoomsFixtures.SetupUserLookup(_users, host, guest);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return (host, guest, room);
    }

    private static void AssertUntouched(params User[] users)
    {
        foreach (var u in users)
        {
            u.Rating.Should().Be(1200);
            u.GamesPlayed.Should().Be(0);
            u.Wins.Should().Be(0);
            u.Losses.Should().Be(0);
            u.Draws.Should().Be(0);
        }
    }

    private MakeMoveCommandHandler MakeMove() => new(
        _rooms.Object, GomokuRules.Registry, _users.Object, _clock.Object, _uow.Object,
        _notifier.Object, RoomsFixtures.TestGameOptions());

    private ResignCommandHandler Resign() => new(
        _rooms.Object, _users.Object, GomokuRules.Registry, _clock.Object, _uow.Object,
        _notifier.Object, RoomsFixtures.TestGameOptions());

    private TurnTimeoutCommandHandler Timeout(int seconds = 60) => new(
        _rooms.Object, _users.Object, GomokuRules.Registry, _clock.Object, _uow.Object,
        _notifier.Object, RoomsFixtures.TestGameOptions(seconds));

    /// <summary>下满一行让黑方三连获胜,返回最后一手的坐标。</summary>
    private static Position PlayToBlackWin(Room room, User host, User guest)
    {
        // X X _
        // O O .
        // . . .      黑方走 (0,2) 成三连。
        var t = RoomsFixtures.Now;
        room.PlayMove(host.Id, new Position(0, 0), t.AddSeconds(1), TicTacToe);
        room.PlayMove(guest.Id, new Position(1, 0), t.AddSeconds(2), TicTacToe);
        room.PlayMove(host.Id, new Position(0, 1), t.AddSeconds(3), TicTacToe);
        room.PlayMove(guest.Id, new Position(1, 1), t.AddSeconds(4), TicTacToe);
        return new Position(0, 2);
    }

    // ---- 路径 ① 落子成胜负 ----

    [Fact]
    public async Task A_win_in_an_unrated_game_moves_no_rating()
    {
        var (host, guest, room) = TicTacToeRoom();
        var winning = PlayToBlackWin(room, host, guest);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddSeconds(5));

        await MakeMove().Handle(
            new MakeMoveCommand(host.Id, room.Id, winning.Row, winning.Col), default);

        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.Result.Should().Be(GameResult.BlackWin);
        room.Game.EndReason.Should().NotBeNull();
        AssertUntouched(host, guest);
    }

    [Fact]
    public async Task The_game_end_event_still_fires_for_an_unrated_game()
    {
        // 不计分 MUST NOT 让对局悄悄结束 —— 客户端靠这个事件收场。
        var (host, guest, room) = TicTacToeRoom();
        var winning = PlayToBlackWin(room, host, guest);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddSeconds(5));

        await MakeMove().Handle(
            new MakeMoveCommand(host.Id, room.Id, winning.Row, winning.Col), default);

        _notifier.Verify(
            n => n.GameEndedAsync(room.Id, It.IsAny<GameEndedDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_unrated_game_does_not_load_the_players_for_rating()
    {
        // 比"评分没变"更强:不计分时连加载 User 这一步都该省掉,不会出现
        // "加载了、算了、又没写回去"的中间状态。
        //
        // 断言的**不是** Times.Never:handler 无论计不计分都要为 DTO 拼用户名,而
        // LookupUsernamesAsync 是 IUserRepository 上的扩展方法,内部逐个 FindByIdAsync。
        // 所以基线是 2 次(黑 + 白)。计分路径会额外加载同样两个人,共 4 次 —— 差值才是
        // 「有没有为了算分去读人」的证据。(第一版这条写成 Times.Never,直接挂了。)
        var (host, guest, room) = TicTacToeRoom();
        var winning = PlayToBlackWin(room, host, guest);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddSeconds(5));

        await MakeMove().Handle(
            new MakeMoveCommand(host.Id, room.Id, winning.Row, winning.Col), default);

        _users.Verify(
            u => u.FindByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task A_rated_game_loads_the_players_twice_over()
    {
        // 上一条的对照组,把那个"差值"钉住:计分路径 = 2 次算分 + 2 次拼用户名。
        // 若哪天用户名查询被换成一次批量往返,这两条会一起挂 —— 那时该一起更新,
        // 而不是只改能过的那条。
        var host = RoomsFixtures.NewUser("Alice");
        var guest = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, guest, "gomoku game", GameKeys.Gomoku);
        RoomsFixtures.SetupUserLookup(_users, host, guest);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddMinutes(1));

        await Resign().Handle(new ResignCommand(host.Id, room.Id), default);

        _users.Verify(
            u => u.FindByIdAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    // ---- 路径 ② 和棋 ----

    [Fact]
    public async Task A_draw_in_an_unrated_game_moves_no_rating()
    {
        var (host, guest, room) = TicTacToeRoom();
        var t = RoomsFixtures.Now;

        // 目标终局(最后一手 (2,2) 由黑走,满盘无三连 → 和棋):
        //   X O X
        //   X O O
        //   O X _
        // 落子顺序必须黑白交替,所以不能按阅读顺序摆 —— 下面是真实的行棋序。
        var order = new (UserId Who, int R, int C)[]
        {
            (host.Id, 0, 0), (guest.Id, 0, 1), (host.Id, 0, 2),
            (guest.Id, 1, 1), (host.Id, 1, 0), (guest.Id, 1, 2),
            (host.Id, 2, 1), (guest.Id, 2, 0),
        };

        var i = 1;
        foreach (var (who, r, c) in order)
        {
            room.PlayMove(who, new Position(r, c), t.AddSeconds(i++), TicTacToe);
        }

        RoomsFixtures.SetupClock(_clock, t.AddSeconds(20));

        await MakeMove().Handle(new MakeMoveCommand(host.Id, room.Id, 2, 2), default);

        room.Game!.Result.Should().Be(GameResult.Draw);
        room.Status.Should().Be(RoomStatus.Finished);
        AssertUntouched(host, guest);
    }

    // ---- 路径 ③ 认输 ----

    [Fact]
    public async Task Resigning_an_unrated_game_moves_no_rating()
    {
        // 这一条是 tasks §6.2 点名"最容易漏"的那个:提案里只写了 MakeMoveCommandHandler,
        // 因为 spec 的 requirement 挂在那儿 —— 但认输是另一条独立的结束路径。
        var (host, guest, room) = TicTacToeRoom();
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddMinutes(1));

        await Resign().Handle(new ResignCommand(host.Id, room.Id), default);

        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.Result.Should().Be(GameResult.WhiteWin);
        room.Game.EndReason.Should().Be(GameEndReason.Resigned);
        AssertUntouched(host, guest);
    }

    // ---- 路径 ④ 超时 ----

    [Fact]
    public async Task Timing_out_an_unrated_game_moves_no_rating()
    {
        var (host, guest, room) = TicTacToeRoom();
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddSeconds(120));

        await Timeout().Handle(new TurnTimeoutCommand(room.Id), default);

        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.EndReason.Should().Be(GameEndReason.TurnTimeout);
        AssertUntouched(host, guest);
    }

    // ---- 不计分不削弱对局记录 ----

    [Fact]
    public async Task An_unrated_game_is_still_fully_replayable()
    {
        // 不计分只是不动评分,对局记录该一样完整。回放 handler 不解析规则(它只回传
        // 走子序列与元数据),所以这里其实不需要任何改动 —— 但 spec 把它写成了规范,
        // 而"没做改动"和"做对了"之间的差别只有测试能说清。
        var (host, guest, room) = TicTacToeRoom();
        var winning = PlayToBlackWin(room, host, guest);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddSeconds(5));
        await MakeMove().Handle(
            new MakeMoveCommand(host.Id, room.Id, winning.Row, winning.Col), default);

        var replay = await new GetGameReplayQueryHandler(_rooms.Object, _users.Object)
            .Handle(new GetGameReplayQuery(room.Id), default);

        replay.Moves.Should().HaveCount(5);
        replay.Result.Should().Be(GameResult.BlackWin);
    }

    // ---- 对照组:五子棋照常计分 ----

    [Fact]
    public async Task A_rated_game_still_moves_rating_on_every_path()
    {
        // 守卫放错了位置(比如无条件早返回)会让这条挂掉 —— 上面那五条都不会。
        var host = RoomsFixtures.NewUser("Alice");
        var guest = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, guest, "gomoku game", GameKeys.Gomoku);
        RoomsFixtures.SetupUserLookup(_users, host, guest);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddMinutes(1));

        await Resign().Handle(new ResignCommand(host.Id, room.Id), default);

        host.Rating.Should().BeLessThan(1200);
        guest.Rating.Should().BeGreaterThan(1200);
        host.Losses.Should().Be(1);
        guest.Wins.Should().Be(1);
    }

    [Fact]
    public async Task An_unresolvable_game_key_skips_rating_instead_of_failing_the_transaction()
    {
        // 对局已经结束并记录在案。为了"算不出分"让整个事务失败,会把一局下完的棋丢掉;
        // 既然无从判断该棋种算不算分,不动评分是保守且可逆的那一侧。
        var host = RoomsFixtures.NewUser("Alice");
        var guest = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, guest, "mystery", "a-game-nobody-registered");
        RoomsFixtures.SetupUserLookup(_users, host, guest);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddMinutes(1));

        var act = () => Resign().Handle(new ResignCommand(host.Id, room.Id), default);

        await act.Should().NotThrowAsync();
        room.Status.Should().Be(RoomStatus.Finished);
        AssertUntouched(host, guest);
    }
}
