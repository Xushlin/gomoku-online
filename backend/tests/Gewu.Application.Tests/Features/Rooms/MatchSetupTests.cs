using System.Collections.Generic;
using Gewu.Application.Features.Rooms.JoinRoom;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Xiangqi;
using Gewu.Domain.Rooms;
using Gewu.Domain.ValueObjects;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// 谁造对局设置、什么时候不造。
/// <para>
/// 熵在 Application 层取,不在 Domain 取:<c>ISeedProvider</c> 就在这一层,而 Domain 不该知道
/// 有一个随机源 —— 所以 <c>Room.JoinAsPlayer</c> 收的是一个**已经造好的字符串**。
/// </para>
/// <para>
/// 走 handler 而不是直接调那个 mapping helper:后者是 <c>internal</c>,而为了测一个两行的
/// 辅助函数把整个 Application 的内部打开给测试程序集,是把测试的便利换成了封装。这与
/// <c>GameEloApplier</c> 一直以来的测法一致 —— 而且这样测到的是**真实路径**。
/// </para>
/// </summary>
public class MatchSetupTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private const string DealtKey = "dealt-probe";

    private sealed class DealtRules : IDealtGameRules
    {
        public string GameKey => DealtKey;
        public int SeatCount => 2;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => false;

        public string CreateSetup(int seed) => $"deal-{seed}";

        public MoveApplication Apply(
            MatchState state, MoveIntent intent, int seat)
            => MoveApplication.Ongoing();
    }

    private sealed class OneRuleRegistry(IGameRules rules) : IGameRulesRegistry
    {
        public IGameRules? For(string gameKey) => gameKey == rules.GameKey ? rules : null;

        public IReadOnlyCollection<IGameRules> All => [rules];
    }

    private async Task<(Room Room, FakeSeeds Seeds)> JoinAsync(
        IGameRulesRegistry registry, string gameKey)
    {
        var host = RoomsFixtures.NewUser("Alice", "alice@example.com");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = Room.Create(RoomId.NewId(), "setup", host.Id, RoomsFixtures.Now, gameKey);
        var seeds = new FakeSeeds(4242);

        RoomsFixtures.SetupClock(_clock);
        RoomsFixtures.SetupUserLookup(_users, host, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new JoinRoomCommandHandler(
            _rooms.Object, _users.Object, _clock.Object, _uow.Object, _notifier.Object,
            RoomsFixtures.TestGameOptions(), registry, seeds);

        await handler.Handle(new JoinRoomCommand(bob.Id, room.Id), default);
        return (room, seeds);
    }

    [Fact]
    public async Task A_dealt_game_takes_one_seed_and_the_setup_lands_on_the_game()
    {
        var (room, seeds) = await JoinAsync(new OneRuleRegistry(new DealtRules()), DealtKey);

        room.Game!.Setup.Should().Be("deal-4242");
        seeds.Calls.Should().Be(1);
    }

    [Fact]
    public async Task A_plain_game_gets_no_setup_and_takes_no_seed()
    {
        // **次数是断言的一部分。** 一个每局都取一次随机数却没人用的调用,会让"这个棋种有
        // 随机性吗"这个问题在读代码时得不到确定答案。
        var (room, seeds) = await JoinAsync(GomokuRules.Registry, GameKeys.Gomoku);

        room.Game!.Setup.Should().BeNull();
        seeds.Calls.Should().Be(0);
    }

    /// <summary>
    /// 第三支:**选定式棋种的设置从房间上取,而且一个种子都不取**。
    /// <para>
    /// 两件事都要断言。只断言「设置对了」的话,一个「取一个种子然后扔掉」的实现照样全绿,
    /// 而它会让「这个棋种有随机性吗」这个问题在读代码时得不到确定答案 —— 与上面那条
    /// 一字不差的理由。
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_positional_game_takes_its_setup_from_the_room_and_takes_no_seed()
    {
        var host = RoomsFixtures.NewUser("Alice", "alice@example.com");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var rules = (IPositionalStartRules)GomokuRules.Registry.For(GameKeys.XiangqiEndgame)!;
        var chosen = ChosenEndgame();
        var room = Room.CreateFromPosition(
            RoomId.NewId(), "setup", host.Id, RoomsFixtures.Now, rules, chosen);
        var seeds = new FakeSeeds(4242);

        RoomsFixtures.SetupClock(_clock);
        RoomsFixtures.SetupUserLookup(_users, host, bob);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new JoinRoomCommandHandler(
            _rooms.Object, _users.Object, _clock.Object, _uow.Object, _notifier.Object,
            RoomsFixtures.TestGameOptions(), GomokuRules.Registry, seeds);
        await handler.Handle(new JoinRoomCommand(bob.Id, room.Id), default);

        room.Game!.Setup.Should().Be(chosen);
        seeds.Calls.Should().Be(0);
    }

    /// <summary>红帅 (9,4)、红车 (9,0);黑将 (0,4)、黑卒 (3,4),黑先走。</summary>
    private static string ChosenEndgame()
    {
        var cells = new char[XiangqiSetup.BoardLength];
        Array.Fill(cells, '.');
        cells[(0 * 9) + 4] = 'k';
        cells[(3 * 9) + 4] = 'p';
        cells[(9 * 9) + 4] = 'K';
        cells[(9 * 9) + 0] = 'R';
        return new XiangqiSetup(new string(cells), FirstSeat: 1).Encode();
    }
}
