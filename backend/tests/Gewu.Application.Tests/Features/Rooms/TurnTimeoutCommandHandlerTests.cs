using System.Collections.Generic;
using Gewu.Application.Features.Rooms.TurnTimeout;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.ValueObjects;

namespace Gewu.Application.Tests.Features.Rooms;

public class TurnTimeoutCommandHandlerTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private TurnTimeoutCommandHandler Build(int turnTimeoutSeconds = 60) => new(
        _rooms.Object, _users.Object, GomokuRules.Registry, _clock.Object, _uow.Object,
        _notifier.Object, RoomsFixtures.TestGameOptions(turnTimeoutSeconds));

    private TurnTimeoutCommandHandler BuildWith(
        IGameRulesRegistry registry, int turnTimeoutSeconds = 60) => new(
        _rooms.Object, _users.Object, registry, _clock.Object, _uow.Object,
        _notifier.Object, RoomsFixtures.TestGameOptions(turnTimeoutSeconds));

    private const string FallbackKey = "fallback-probe";

    /// <summary>超时时替那个座位走一步的两座位探针 —— 不计分,免得牵进 ELO。</summary>
    private sealed class FallbackRules(Func<int, MoveApplication>? apply = null)
        : ITimeoutFallbackRules
    {
        public string GameKey => FallbackKey;
        public int SeatCount => 2;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => false;

        public MoveIntent MoveOnTimeout(IReadOnlyList<PlayedMove> history, int seat)
            => MoveIntent.Place(new Position(history.Count, 0));

        public MoveApplication Apply(
            IReadOnlyList<PlayedMove> history, MoveIntent intent, int seat)
            => (apply ?? (_ => MoveApplication.Ongoing()))(seat);
    }

    private sealed class OneRuleRegistry(IGameRules rules) : IGameRulesRegistry
    {
        public IGameRules? For(string gameKey) => gameKey == rules.GameKey ? rules : null;

        public IReadOnlyCollection<IGameRules> All => [rules];
    }

    private Room FallbackRoom(IGameRules rules)
    {
        var alice = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = Room.Create(RoomId.NewId(), "fallback", alice.Id, RoomsFixtures.Now, FallbackKey);
        room.JoinAsPlayer(bob.Id, RoomsFixtures.Now.AddSeconds(1), rules, setup: null);

        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddSeconds(1).AddSeconds(61));
        RoomsFixtures.SetupUserLookup(_users, alice, bob);
        RoomsFixtures.SetupGameStats(_users);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        return room;
    }

    [Fact]
    public async Task A_fallback_timeout_broadcasts_a_move_and_not_a_game_end()
    {
        // 兜底走出的一步在线上与真人走的一步没有区别 —— 客户端不需要区分"他走的"与
        // "系统替他走的",而房间状态广播本来就带着新的 CurrentTurn。
        var rules = new FallbackRules();
        var room = FallbackRoom(rules);

        await BuildWith(new OneRuleRegistry(rules)).Handle(new TurnTimeoutCommand(room.Id), default);

        room.Status.Should().Be(RoomStatus.Playing);
        room.Game!.Moves.Should().HaveCount(1);
        _notifier.Verify(n => n.MoveMadeAsync(room.Id, It.IsAny<MoveDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.GameEndedAsync(It.IsAny<RoomId>(), It.IsAny<GameEndedDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_fallback_move_that_ends_the_game_broadcasts_both()
    {
        var rules = new FallbackRules(seat => MoveApplication.Won(seat));
        var room = FallbackRoom(rules);

        await BuildWith(new OneRuleRegistry(rules)).Handle(new TurnTimeoutCommand(room.Id), default);

        room.Status.Should().Be(RoomStatus.Finished);
        _notifier.Verify(n => n.MoveMadeAsync(room.Id, It.IsAny<MoveDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.GameEndedAsync(room.Id,
            It.Is<GameEndedDto>(d => d.EndReason == GameEndReason.Decided),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_unknown_game_key_is_a_corrupt_room_record()
    {
        var rules = new FallbackRules();
        var room = FallbackRoom(rules);

        // 注册表里没有这个键 —— 与落子路径同样的处理。
        var act = () => Build().Handle(new TurnTimeoutCommand(room.Id), default);

        await act.Should().ThrowAsync<RoomNotFoundException>();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

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
        var stats = RoomsFixtures.SetupGameStats(_users);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        await Build(60).Handle(new TurnTimeoutCommand(room.Id), default);

        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.Result.Should().Be(GameResult.Decided);
        room.Game.WinnerUserId.Should().Be(room.WhitePlayerId, "先手座位超时 → 后手胜");
        room.Game.EndReason.Should().Be(GameEndReason.TurnTimeout);

        // ELO 变动 —— 落在该棋种的战绩行上
        stats.Of(alice).GamesPlayed.Should().Be(1);
        stats.Of(alice).Losses.Should().Be(1);
        stats.Of(bob).GamesPlayed.Should().Be(1);
        stats.Of(bob).Wins.Should().Be(1);

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notifier.Verify(n => n.GameEndedAsync(room.Id,
            It.Is<GameEndedDto>(d => d.EndReason == GameEndReason.TurnTimeout && d.Result == GameResult.Decided),
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
