using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
using Microsoft.Extensions.Options;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>Rooms handler 测试共用的 builder / mock 设置。</summary>
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

    public static Room PlayingRoom(
        User host, User challenger, string name = "Test Room", string gameKey = GameKeys.Gomoku)
    {
        var room = Room.Create(RoomId.NewId(), name, host.Id, Now, gameKey);
        room.JoinAsPlayer(challenger.Id, Now.AddSeconds(1), BuiltInGameRules.Gomoku);
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
