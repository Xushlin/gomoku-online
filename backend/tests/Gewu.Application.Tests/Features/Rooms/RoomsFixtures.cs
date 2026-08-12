using Microsoft.Extensions.Options;

namespace Gomoku.Application.Tests.Features.Rooms;

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

    public static Room WaitingRoom(User host, string name = "Test Room") =>
        Room.Create(RoomId.NewId(), name, host.Id, Now);

    public static Room PlayingRoom(User host, User challenger, string name = "Test Room")
    {
        var room = Room.Create(RoomId.NewId(), name, host.Id, Now);
        room.JoinAsPlayer(challenger.Id, Now.AddSeconds(1));
        return room;
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
