namespace Gomoku.Application.Tests.Features.Rooms;

/// <summary>Rooms handler 测试共用的 builder / mock 设置。</summary>
internal static class RoomsFixtures
{
    public static readonly DateTime Now = new(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);

    public static User NewUser(string username = "Alice", string email = "alice@example.com") =>
        User.Register(
            UserId.NewId(),
            new Email(email),
            new Username(username),
            "HASHED",
            Now);

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
