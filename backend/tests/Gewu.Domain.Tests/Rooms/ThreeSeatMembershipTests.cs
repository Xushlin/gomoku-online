using System;
using System.Collections.Generic;
using FluentAssertions;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Doudizhu;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Rooms;

/// <summary>
/// 「这个人是不是本房间的玩家」在三个座位下的判定。
/// <para>
/// 这条问题在聚合与 Application 层一共有**六份手写副本**,每一份都写成
/// <c>BlackPlayerId || WhitePlayerId</c> —— 也就是只认 0 号与 1 号座位。斗地主落地之后它们全错,
/// 而 1266 条既有测试**一条都没红**:三座位的这几条路此前没有任何测试走过。
/// </para>
/// <para>
/// 后果不是"少一个字段"。实测(真 HTTP,三个真账号,一个真 doudizhu 房间):
/// <list type="bullet">
/// <item>2 号座位 <c>POST /leave</c> → <b>404</b>,他离不开自己在的房间;</item>
/// <item>2 号座位 <c>POST /spectate</c> → <b>204</b>,一个占着座位的玩家成了围观者 ——
/// 于是他拿到围观视角与围观频道,正是 <c>fix-spectator-chat-leak</c> 建起来的那条不变量。</item>
/// </list>
/// </para>
/// <para>
/// <c>SeatOf</c> 的文档说它存在是因为"三处需要'这人是第几号'的地方各写了一遍同样的 if/else"。
/// **收敬只做了一半**:"他是几号"进了一处,"他是不是玩家"仍然散在六处 —— 而那是同一个事实的
/// 两种问法。
/// </para>
/// </summary>
public class ThreeSeatMembershipTests
{
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DoudizhuRules Rules = new();

    private const int Seed = 20260819;

    /// <summary>一个坐满三个人、已开局的斗地主房间。</summary>
    private static (Room Room, List<UserId> Players) PlayingRoom()
    {
        var host = new UserId(Guid.NewGuid());
        var room = Room.Create(new RoomId(Guid.NewGuid()), "ddz", host, Now, GameKeys.Doudizhu);
        var players = new List<UserId> { host };

        var second = new UserId(Guid.NewGuid());
        room.JoinAsPlayer(second, Now.AddSeconds(1), Rules, setup: null);
        players.Add(second);

        var third = new UserId(Guid.NewGuid());
        room.JoinAsPlayer(third, Now.AddSeconds(2), Rules, setup: Rules.CreateSetup(Seed));
        players.Add(third);

        room.Status.Should().Be(RoomStatus.Playing);
        return (room, players);
    }

    [Fact]
    public void The_third_player_is_a_player()
    {
        var (room, players) = PlayingRoom();

        room.IsPlayer(players[2]).Should().BeTrue();
        room.SeatOf(players[2]).Should().Be(2, "他确实占着 2 号座位 —— 两个说法必须一致");
    }

    [Fact]
    public void The_third_player_can_leave_the_room_they_are_in()
    {
        // 实测过的那个 404 的领域侧原因。
        var (room, players) = PlayingRoom();

        var act = () => room.Leave(players[2], Now.AddMinutes(1));

        act.Should().NotThrow<NotInRoomException>();
        room.Status.Should().Be(RoomStatus.Playing, "对局中玩家离席不改房间状态");
        room.SeatOf(players[2]).Should().Be(2, "离席不腾出座位 —— 这一条本变更不改");
    }

    [Fact]
    public void The_third_player_cannot_spectate_their_own_game()
    {
        // 实测过的那个 204。`JoinAsSpectator` 一直显式拒绝黑白两方,所以拦住玩家是**既有意图**,
        // 只是它数不到 2 号座位 —— 这是缺陷,不是决定。
        var (room, players) = PlayingRoom();

        var act = () => room.JoinAsSpectator(players[2]);

        act.Should().Throw<PlayerCannotSpectateException>();
        room.Spectators.Should().BeEmpty();
    }

    [Fact]
    public void The_third_player_cannot_post_to_the_spectator_channel()
    {
        // **围观频道那条规则的写入侧。** `fix-spectator-chat-leak` 的结论是"写入侧一直是强制的,
        // 漏的是三条读取路径" —— 那句话对两座位成立,对三座位不成立。
        var (room, players) = PlayingRoom();

        var act = () => room.PostChatMessage(
            players[2], "third", "偷看一下", ChatChannel.Spectator, Now.AddMinutes(1));

        act.Should().Throw<PlayerCannotPostSpectatorChannelException>();
    }

    [Fact]
    public void The_third_player_can_post_to_the_room_channel()
    {
        // 反面控制:上面那条不是"2 号座位什么都发不了"。
        var (room, players) = PlayingRoom();

        var message = room.PostChatMessage(
            players[2], "third", "该我了吗", ChatChannel.Room, Now.AddMinutes(1));

        message.Channel.Should().Be(ChatChannel.Room);
        room.ChatMessages.Should().ContainSingle();
    }

    [Fact]
    public void An_outsider_is_still_refused_everywhere()
    {
        // 把判据放宽到"任何座位"之后,**真正的外人必须仍然被拒** —— 否则这个修法就是把
        // 一个漏洞换成一个更大的洞。
        var (room, _) = PlayingRoom();
        var stranger = new UserId(Guid.NewGuid());

        room.IsPlayer(stranger).Should().BeFalse();
        ((Action)(() => room.Leave(stranger, Now.AddMinutes(1))))
            .Should().Throw<NotInRoomException>();
        ((Action)(() => room.PostChatMessage(
            stranger, "x", "hi", ChatChannel.Room, Now.AddMinutes(1))))
            .Should().Throw<NotInRoomException>();
        room.JoinAsSpectator(stranger);
        room.Spectators.Should().ContainSingle().Which.Should().Be(stranger);
    }

    [Fact]
    public void Urging_targets_whoever_is_to_move_not_seat_zero()
    {
        // 这一条是本变更里唯一一处**行为**的一般化,而不是判定的修正。
        //
        // 原式是 `senderSeat == 0 ? WhitePlayerId! : BlackPlayerId` —— 两座位下它等价于
        // "催该走棋的那个人"(下面那条守卫保证发起人不是当前回合),三座位下它**永远催 0 号**,
        // 而 2 号座位永远催不到。
        //
        // 判别用的局面必须是"该走棋的人既不是 0 号也不是发起人",否则新旧两式的答案相同、
        // 这条测试就什么都没证。叫两轮不叫把出手权推到 2 号座位上。
        var (room, players) = PlayingRoom();
        room.PlayMove(players[0], MoveIntent.Say("bid:0"), Now.AddSeconds(10), Rules);
        room.PlayMove(players[1], MoveIntent.Say("bid:0"), Now.AddSeconds(11), Rules);
        room.Game!.CurrentTurn.Should().Be(2);

        var outcome = room.UrgeOpponent(players[0], Now.AddSeconds(12), cooldownSeconds: 0);

        outcome.UrgedUser.Should().Be(players[2], "催的是该走棋的人");
        outcome.UrgedUser.Should().NotBe(players[1], "原式会催到 1 号座位(WhitePlayerId)");
    }

    [Fact]
    public void Urging_in_a_two_seat_game_is_unchanged()
    {
        // 上面那条一般化对两座位棋种必须是**零行为改动**。这里用真五子棋房间验。
        var host = new UserId(Guid.NewGuid());
        var room = Room.Create(new RoomId(Guid.NewGuid()), "gomoku", host, Now, GameKeys.Gomoku);
        var white = new UserId(Guid.NewGuid());
        var gomoku = Gewu.Domain.Games.NInARow.BuiltInGameRules.Gomoku;
        room.JoinAsPlayer(white, Now.AddSeconds(1), gomoku, setup: null);

        // 黑先,所以此刻该黑走 —— 白方催的是黑方。
        var outcome = room.UrgeOpponent(white, Now.AddSeconds(2), cooldownSeconds: 0);

        outcome.UrgedUser.Should().Be(host);
    }
}
