using Gewu.Application.Common.Mapping;
using Gewu.Application.Features.Rooms.LeaveRoom;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Doudizhu;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// 三座位房间在 Application 层的成员判定。
/// <para>
/// 「这个人是不是玩家」的第四份与第五份手写副本住在这一层:<c>LeaveRoomCommandHandler</c> 自己
/// 列举黑白两个座位,而 <c>RoomMapping.CollectUserIds</c> 只收黑白两个人的 id。两者都只认
/// 0 号与 1 号。领域侧的四份见 <c>ThreeSeatMembershipTests</c>。
/// </para>
/// </summary>
public class ThreeSeatRoomHandlersTests
{
    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    private static readonly DoudizhuRules Rules = new();

    private LeaveRoomCommandHandler Build() => new(
        _rooms.Object, _users.Object, _clock.Object, _uow.Object, _notifier.Object,
        RoomsFixtures.TestGameOptions());

    /// <summary>一个坐满三个人的斗地主房间,连同三个 <c>User</c>。</summary>
    private static (Room Room, User[] Users) DoudizhuRoom()
    {
        var a = RoomsFixtures.NewUser("Alice");
        var b = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var c = RoomsFixtures.NewUser("Carol", "carol@example.com");
        var room = Room.Create(RoomId.NewId(), "ddz", a.Id, RoomsFixtures.Now, GameKeys.Doudizhu);
        room.JoinAsPlayer(b.Id, RoomsFixtures.Now.AddSeconds(1), Rules, setup: null);
        room.JoinAsPlayer(c.Id, RoomsFixtures.Now.AddSeconds(2), Rules, setup: Rules.CreateSetup(20260819));
        return (room, [a, b, c]);
    }

    [Fact]
    public async Task The_third_player_leaving_is_broadcast_as_a_player_leaving()
    {
        // 修之前 `wasPlayer` 与 `wasSpectator` 对 2 号座位**双双为 false**,于是
        // 两个事件一个都不发 —— 房间里没有人知道第三个人走了。
        var (room, users) = DoudizhuRoom();
        RoomsFixtures.SetupClock(_clock);
        RoomsFixtures.SetupUserLookup(_users, users);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        await Build().Handle(new LeaveRoomCommand(users[2].Id, room.Id), default);

        _notifier.Verify(n => n.PlayerLeftAsync(room.Id,
            It.Is<UserSummaryDto>(u => u.Id == users[2].Id.Value), It.IsAny<CancellationToken>()),
            Times.Once);
        _notifier.Verify(n => n.SpectatorLeftAsync(It.IsAny<RoomId>(), It.IsAny<UserSummaryDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Collecting_user_ids_includes_every_seat()
    {
        // 漏掉 2 号座位的后果不是缺一个字段,是**那个人的用户名查不到** ——
        // 显示出来会是 `<unknown>`,而那看起来像一个数据损坏,不像一个漏掉的座位。
        var (room, users) = DoudizhuRoom();

        var ids = room.CollectUserIds();

        ids.Should().Contain(users[2].Id.Value);
        ids.Should().HaveCount(3, "三个座位三个人,host 与 0 号座位是同一个人");
    }

    [Fact]
    public void Collecting_user_ids_still_includes_spectators_and_the_host()
    {
        // 反面控制:改成遍历座位之后,原来收得到的那两类人不能丢。
        var (room, users) = DoudizhuRoom();
        var watcher = RoomsFixtures.NewUser("Dave", "dave@example.com");
        room.JoinAsSpectator(watcher.Id);

        var ids = room.CollectUserIds();

        ids.Should().Contain(room.HostUserId.Value);
        ids.Should().Contain(watcher.Id.Value);
        ids.Should().HaveCount(4);
    }
}
