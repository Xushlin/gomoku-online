using System.Collections.Generic;
using Gewu.Application.Features.Rooms.MakeMove;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// 结算评分时,<c>Decided</c> 而赢家不属于两位玩家 MUST 抛,MUST NOT 任选一方判胜。
/// <para>
/// **这条测试是变异测试逼出来的。** 把 <c>GameEloApplier</c> 里那条
/// <c>when w == whiteId</c> 改成通配 <c>_</c>(于是任何未知赢家都算白方输),1165 条测试
/// 一条都不红 —— 那个守卫是我在这次改动里新写的,而我一开始没给它测试。
/// </para>
/// <para>
/// 它守的是"聚合出了错"这种状态:静默算一遍分,会把一个错误的赢家写进两个人的 ELO 与战绩,
/// 而那是**不可逆**的 —— 排行榜没有"这一局其实不算"的记录。
/// </para>
/// </summary>
public class EloWinnerGuardTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private const string ThreeSeatKey = "three-seat-probe";

    /// <summary>
    /// 一个**计分的三座位**探针规则,判 2 号座位赢。
    /// <para>
    /// 这是构造"赢家不是那两位玩家"最诚实的方式 —— 不用反射去改私有字段,而是让聚合走它
    /// 正常的路径:2 号座位赢了,<c>WinnerUserId</c> 就是 2 号座位上的人,而结算只看 0 号与 1 号。
    /// </para>
    /// <para>
    /// 生产里 <c>IsRated ⇒ SeatCount == 2</c> 由一条遍历注册表的测试守着,所以这个组合只可能
    /// 出现在这里 —— 那正是它作为探针的用途:它演的是那条不变量被破坏之后的世界。
    /// </para>
    /// </summary>
    private sealed class RatedThreeSeatRules : IGameRules
    {
        public string GameKey => ThreeSeatKey;
        public int SeatCount => 3;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => true;

        public MoveApplication Apply(
            IReadOnlyList<PlayedMove> history, MoveIntent intent, int seat)
            => MoveApplication.Won(2);
    }

    /// <summary>只登记那个探针的注册表。</summary>
    private sealed class OneRuleRegistry(IGameRules rules) : IGameRulesRegistry
    {
        public IGameRules? For(string gameKey) => gameKey == rules.GameKey ? rules : null;

        public IReadOnlyCollection<IGameRules> All => [rules];
    }

    [Fact]
    public async Task A_winner_who_is_neither_player_is_refused()
    {
        var rules = new RatedThreeSeatRules();
        var registry = new OneRuleRegistry(rules);

        var host = RoomsFixtures.NewUser("Alice");
        var second = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var third = RoomsFixtures.NewUser("Carol", "carol@example.com");

        var room = Room.Create(RoomId.NewId(), "three", host.Id, RoomsFixtures.Now, ThreeSeatKey);
        room.JoinAsPlayer(second.Id, RoomsFixtures.Now.AddSeconds(1), rules, setup: null);
        room.JoinAsPlayer(third.Id, RoomsFixtures.Now.AddSeconds(2), rules, setup: null);
        room.Status.Should().Be(RoomStatus.Playing, "三个座位坐满才开局");

        RoomsFixtures.SetupUserLookup(_users, host, second, third);
        RoomsFixtures.SetupGameStats(_users);
        RoomsFixtures.SetupClock(_clock);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new MakeMoveCommandHandler(
            _rooms.Object, registry, _users.Object, _clock.Object, _uow.Object,
            _notifier.Object, RoomsFixtures.TestGameOptions());

        var act = async () => await handler.Handle(
            new MakeMoveCommand(host.Id, room.Id, 0, 0), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();

        // 没有一次提交 —— 抛在 SaveChanges 之前,所以那盘棋的记录与两个人的评分都没落库。
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
