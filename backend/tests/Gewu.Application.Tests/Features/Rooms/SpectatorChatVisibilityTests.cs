using Gewu.Application.Common.Mapping;
using Gewu.Domain.Games.NInARow;
using Gewu.Application.Features.Rooms.GetRoomRole;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// 围观频道**仅围观者可见**在读取侧的执行点。
/// <para>
/// `in-room-chat` 的这条规则,写入侧一直是强制的(玩家发围观频道抛
/// <c>PlayerCannotPostSpectatorChannelException</c>),而读取侧**三条路曾经全部泄漏**:
/// REST 快照、`RoomState` 广播、以及实时 `ChatMessage` 事件 —— 最后那条的分群是对的,
/// 但 <c>JoinSpectatorGroup</c> 不做任何校验,玩家自己调它就进了围观子群。
/// </para>
/// <para>
/// 三条都是在浏览器 + 真实 SignalR 连接上量出来的,不是读代码推断的。这些用例是那次实测的
/// 可执行形式,钉住的是**投影**与**身份解析**两件事;分群推送本身由 `SignalRRoomNotifier`
/// 承担,而它现在收原料自己投影两份,调用方没有忘记裁剪的机会。
/// </para>
/// </summary>
public class SpectatorChatVisibilityTests
{
    private static readonly DateTime Now = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IReadOnlyDictionary<Guid, string> NoNames = new Dictionary<Guid, string>();

    private static (Room Room, UserId Black, UserId White, UserId Fan1, UserId Fan2, UserId Stranger) Watched()
    {
        var black = UserId.NewId();
        var white = UserId.NewId();
        var fan1 = UserId.NewId();
        var fan2 = UserId.NewId();
        var stranger = UserId.NewId();

        var room = Room.Create(RoomId.NewId(), "watched", black, Now, GameKeys.Gomoku);
        room.JoinAsPlayer(white, Now.AddSeconds(1), BuiltInGameRules.Gomoku, setup: null);
        room.JoinAsSpectator(fan1);
        room.JoinAsSpectator(fan2);

        room.PostChatMessage(black, "Black", "good luck", ChatChannel.Room, Now.AddSeconds(2));
        room.PostChatMessage(fan1, "Fan1", "黑方这步不行", ChatChannel.Spectator, Now.AddSeconds(3));
        room.PostChatMessage(fan2, "Fan2", "我押白方", ChatChannel.Spectator, Now.AddSeconds(4));

        return (room, black, white, fan1, fan2, stranger);
    }

    private static IReadOnlyList<ChatChannel> ChannelsSeenBy(Room room, UserId viewer)
        => [.. room.ToState(NoNames, 60, RoomView.For(room, viewer)).ChatMessages.Select(m => m.Channel)];

    [Fact]
    public void A_player_never_receives_spectator_channel_messages()
    {
        // 这是整组里唯一真正重要的一条。它此前是假的:玩家一次 GET /api/rooms/{id}
        // 就拿到了对手围观区的全部内容,而屏幕上看不出来 —— ChatPanel 藏了那个 Tab。
        var (room, black, white, _, _, _) = Watched();

        ChannelsSeenBy(room, black).Should().AllSatisfy(c => c.Should().Be(ChatChannel.Room));
        ChannelsSeenBy(room, white).Should().AllSatisfy(c => c.Should().Be(ChatChannel.Room));
    }

    [Fact]
    public void A_player_still_sees_the_room_channel()
    {
        // 裁剪必须只裁围观频道。把房间聊天一起裁掉是"修好了但坏了别的"。
        var (room, black, _, _, _, _) = Watched();

        room.ToState(NoNames, 60, RoomView.For(room, black))
            .ChatMessages.Should().ContainSingle()
            .Which.Content.Should().Be("good luck");
    }

    [Fact]
    public void Both_spectators_see_both_channels_including_each_others_comments()
    {
        var (room, _, _, fan1, fan2, _) = Watched();

        foreach (var fan in new[] { fan1, fan2 })
        {
            var seen = room.ToState(NoNames, 60, RoomView.For(room, fan)).ChatMessages;
            seen.Should().HaveCount(3);
            seen.Where(m => m.Channel == ChatChannel.Spectator)
                .Select(m => m.SenderUsername)
                .Should().BeEquivalentTo(["Fan1", "Fan2"]);
        }
    }

    [Fact]
    public void Someone_not_yet_seated_does_not_see_the_spectator_channel()
    {
        // 判据是"是不是围观者",不是"不是玩家"。
        //
        // 我第一版写的是后者,这条用例当时断言的正是相反的结果(旁观者看得到)。它让 REST 与
        // 广播不一致:REST 给他围观频道,而广播分组里他既不在 players 也不在 spectators ——
        // 两个组不穷尽,他一份快照都收不到。取"是围观者"这一侧同时修好了两件事。
        //
        // 代价是"先看看再决定围观"看不到评论,而那一步点一下大厅的「围观」按钮就跨过去了。
        var (room, _, _, _, _, stranger) = Watched();

        room.ToState(NoNames, 60, RoomView.For(room, stranger)).ChatMessages
            .Should().ContainSingle().Which.Channel.Should().Be(ChatChannel.Room);
    }

    [Fact]
    public void The_players_broadcast_view_is_trimmed_and_the_spectators_one_is_not()
    {
        // 广播分两份。`SignalRRoomNotifier` 收原料自己投影,所以调用方没有忘记裁剪的机会 ——
        // 而"忘记"正是此前的形状:一份 DTO 推给整个 room group。
        var (room, _, _, _, _, _) = Watched();

        room.ToState(NoNames, 60, RoomView.ForNonSpectators).ChatMessages.Should().HaveCount(1);
        room.ToState(NoNames, 60, RoomView.ForSpectators).ChatMessages.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_role_used_for_subgroup_assignment_comes_from_the_aggregate(bool asPlayer)
    {
        // 子群分配此前取自**客户端自报**(前端调 JoinSpectatorGroup),于是玩家把自己塞进
        // 围观子群就能实时收到吐槽。现在取自聚合。
        var (room, black, _, fan1, _, _) = Watched();
        var rooms = new Mock<IRoomRepository>();
        rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);

        var sut = new GetRoomRoleQueryHandler(rooms.Object);
        var role = await sut.Handle(new GetRoomRoleQuery(asPlayer ? black : fan1, room.Id), default);

        role.Should().Be(asPlayer ? RoomRole.Player : RoomRole.Spectator);
    }

    [Fact]
    public async Task A_stranger_gets_no_role_and_a_missing_room_does_not_throw()
    {
        var (room, _, _, _, _, stranger) = Watched();
        var rooms = new Mock<IRoomRepository>();
        rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        rooms.Setup(r => r.FindByIdAsync(It.Is<RoomId>(i => i != room.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Room?)null);

        var sut = new GetRoomRoleQueryHandler(rooms.Object);

        (await sut.Handle(new GetRoomRoleQuery(stranger, room.Id), default))
            .Should().Be(RoomRole.None);

        // 房间没了时分群逻辑的正确反应是"不加任何子群",而不是把建连变成错误路径。
        (await sut.Handle(new GetRoomRoleQuery(stranger, RoomId.NewId()), default))
            .Should().Be(RoomRole.None);
    }
}
