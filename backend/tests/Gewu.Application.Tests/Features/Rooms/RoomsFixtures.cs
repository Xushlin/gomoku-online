using Gewu.Domain.Games.Doudizhu;
using Gewu.Domain.ValueObjects;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
using Microsoft.Extensions.Options;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>Rooms handler 测试共用的 builder / mock 设置。</summary>
/// <summary>
/// 固定种子的 <see cref="ISeedProvider"/> —— 顺带**数一数它被调了几次**。
/// <para>
/// 次数是要断言的东西之一:不需要设置的棋种 MUST NOT 取随机数。一个每局都取一次随机数
/// 却没人用的调用,会让"这个棋种有随机性吗"这个问题在读代码时得不到确定答案。
/// </para>
/// </summary>
internal sealed class FakeSeeds(int seed = 20260819) : ISeedProvider
{
    /// <summary>被调用了几次。</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public int NextSeed()
    {
        Calls++;
        return seed;
    }
}

internal static class RoomsFixtures
{
    public static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// 生成一个测试用的 <see cref="IOptions{GameOptions}"/>,默认 60s turn timeout / 5s poll。
    /// </summary>
    public static IOptions<GameOptions> TestGameOptions(int turnTimeoutSeconds = 60, int timeoutPollIntervalMs = 5000) =>
        Options.Create(new GameOptions
        {
            TurnTimeoutSeconds = turnTimeoutSeconds,
            TimeoutPollIntervalMs = timeoutPollIntervalMs,
        });

    public static User NewUser(string username = "Alice", string email = "alice@example.com") =>
        User.Register(
            UserId.NewId(),
            new Email(email),
            new Username(username),
            "HASHED",
            Now);

    /// <summary>
    /// 构造一个 bot User,使用 <see cref="BotAccountIds"/> 的固定 Guid,字段与 seed 迁移产物一致。
    /// </summary>
    public static User NewBot(BotDifficulty difficulty)
    {
        var id = new UserId(BotAccountIds.For(difficulty));
        var suffix = difficulty.ToString().ToLowerInvariant();
        return User.RegisterBot(
            id,
            new Email($"{suffix}@bot.gomoku.local"),
            new Username($"AI_{difficulty}"),
            Now);
    }

    public static Room WaitingRoom(
        User host, string name = "Test Room", string gameKey = GameKeys.Gomoku) =>
        Room.Create(RoomId.NewId(), name, host.Id, Now, gameKey);

    /// <summary>
    /// 一局**真的**打完的斗地主:三个座位坐满,地主把 20 张牌一张一张出光。
    /// <para>
    /// 住在这里而不是某个测试类里,因为第二个消费方到了(回放 + 战绩)。**一份复制品会分叉,
    /// 而症状是两个套件对"三座位对局长什么样"给出不同的答案。**
    /// </para>
    /// <para>
    /// 三座位样本不能用「造一个假 Room」凑 —— 这里要证的正是 handler 从真聚合里读座位,
    /// 而一个手工塞进去的座位列表会把 <c>Room.Seats</c> 这一环跳过去。出牌脚本抄自
    /// <c>DoudizhuThroughRoomTests</c>:过牌总是合法,所以它不依赖那副牌里谁能压谁。
    /// </para>
    /// </summary>
    public static (Room Room, User[] Users) FinishedDoudizhuRoom()
    {
        var rules = new DoudizhuRules();
        var alice = NewUser("Alice");
        var bob = NewUser("Bob", "bob@example.com");
        var carol = NewUser("Carol", "carol@example.com");
        var room = Room.Create(RoomId.NewId(), "ddz-replay", alice.Id, Now, GameKeys.Doudizhu);
        room.JoinAsPlayer(bob.Id, Now.AddSeconds(1), rules, setup: null);
        room.JoinAsPlayer(carol.Id, Now.AddSeconds(2), rules, setup: rules.CreateSetup(20260819));

        var t = 10;
        room.PlayMove(alice.Id, MoveIntent.Say("bid:3"), Now.AddSeconds(t++), rules);
        // 地主手上是 17 张 + 3 张底牌 = 20 张。写成两个常量相加而不是字面量 20:
        // 一个字面量在牌数改动时不会红,只会**打不完**,而症状是「测试卡在中间」。
        const int landlordCards = DoudizhuDeal.HandSize + DoudizhuDeal.KittySize;
        for (var played = 0; played < landlordCards; played++)
        {
            var hand = DoudizhuTable.Reconstruct(room.Game!.State()).HandOf(0);
            room.PlayMove(alice.Id, MoveIntent.Say($"play:{hand[0].Encode()}"), Now.AddSeconds(t++), rules);
            if (played == landlordCards - 1) break;
            room.PlayMove(bob.Id, MoveIntent.Say("pass"), Now.AddSeconds(t++), rules);
            room.PlayMove(carol.Id, MoveIntent.Say("pass"), Now.AddSeconds(t++), rules);
        }

        return (room, [alice, bob, carol]);
    }

    public static Room PlayingRoom(
        User host, User challenger, string name = "Test Room", string gameKey = GameKeys.Gomoku)
    {
        var room = Room.Create(RoomId.NewId(), name, host.Id, Now, gameKey);
        room.JoinAsPlayer(challenger.Id, Now.AddSeconds(1), BuiltInGameRules.Gomoku, setup: null);
        return room;
    }

    /// <summary>
    /// 让 mock 的 <c>GetOrCreateGameStatsAsync</c> 表现得像真仓库:同一 <c>(userId, gameKey)</c>
    /// 每次返回同一实例,不存在则以初始值新建。返回那本账本,测试可以直接查行、数行。
    /// <para>
    /// 一个纯 <c>ReturnsAsync(UserGameStats.Start(...))</c> 的 stub 在这里是不够的:ELO 路径要
    /// 连着取黑白两方、还要能观察到"这一局到底建了几行",而"不计分棋种一行都不该建"正是要断言的东西。
    /// </para>
    /// </summary>
    public static FakeGameStats SetupGameStats(Mock<IUserRepository> mock)
    {
        var store = new FakeGameStats();
        mock.Setup(r => r.GetOrCreateGameStatsAsync(
                It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserId id, string gameKey, CancellationToken _) => store.GetOrCreate(id, gameKey));
        mock.Setup(r => r.FindGameStatsAsync(
                It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserId id, string gameKey, CancellationToken _) => store.Find(id, gameKey));
        return store;
    }

    public static void SetupUserLookup(Mock<IUserRepository> mock, params User[] users)
    {
        foreach (var u in users)
        {
            mock.Setup(r => r.FindByIdAsync(u.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(u);
        }
    }

    public static void SetupClock(Mock<IDateTimeProvider> mock, DateTime? now = null)
    {
        mock.SetupGet(c => c.UtcNow).Returns(now ?? Now);
    }
}
