using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Rooms;

/// <summary>
/// 房间的座位是一个集合,不再是两个字段(<c>add-room-seats</c>)。
/// </summary>
public class RoomSeatsTests
{
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>座位数可配的探针规则;永远 <c>Ongoing</c>,本文件只关心座位与轮转。</summary>
    private sealed class SeatsRules(int seatCount) : IGameRules
    {
        public string GameKey => "seats-probe";
        public int SeatCount { get; } = seatCount;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => false;

        public MoveApplication Apply(
            IReadOnlyList<PlayedMove> history, MoveIntent intent, int seat)
            => MoveApplication.Ongoing();
    }

    private static UserId NewUser() => new(Guid.NewGuid());

    private static Room WaitingRoom(UserId host) =>
        Room.Create(new RoomId(Guid.NewGuid()), "seats", host, Now, "seats-probe");

    [Fact]
    public void The_host_takes_seat_zero_at_creation()
    {
        var host = NewUser();

        var room = WaitingRoom(host);

        room.Seats.Should().HaveCount(1);
        room.PlayerAt(0).Should().Be(host);
        room.SeatOf(host).Should().Be(0);
        room.BlackPlayerId.Should().Be(host, "两人棋种的黑方就是 0 号座位");
        room.WhitePlayerId.Should().BeNull();
    }

    [Fact]
    public void A_two_seat_game_starts_when_the_second_player_sits()
    {
        var host = NewUser();
        var room = WaitingRoom(host);
        var guest = NewUser();

        room.JoinAsPlayer(guest, Now.AddSeconds(1), new SeatsRules(2));

        room.Status.Should().Be(RoomStatus.Playing);
        room.Game.Should().NotBeNull();
        room.WhitePlayerId.Should().Be(guest);
    }

    [Fact]
    public void A_three_seat_game_stays_waiting_until_the_third_player_sits()
    {
        var rules = new SeatsRules(3);
        var host = NewUser();
        var room = WaitingRoom(host);

        room.JoinAsPlayer(NewUser(), Now.AddSeconds(1), rules);

        // 这是本变更新增的状态:坐进来了,但还没满。两人棋种下这一步就开局了。
        room.Status.Should().Be(RoomStatus.Waiting);
        room.Game.Should().BeNull();
        room.Seats.Should().HaveCount(2);

        room.JoinAsPlayer(NewUser(), Now.AddSeconds(2), rules);

        room.Status.Should().Be(RoomStatus.Playing);
        room.Game.Should().NotBeNull();
        room.Seats.Select(s => s.Index).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void A_three_seat_game_walks_the_whole_ring_through_the_real_aggregate()
    {
        var rules = new SeatsRules(3);
        var host = NewUser();
        var second = NewUser();
        var third = NewUser();
        var room = WaitingRoom(host);
        room.JoinAsPlayer(second, Now.AddSeconds(1), rules);
        room.JoinAsPlayer(third, Now.AddSeconds(2), rules);

        var turns = new List<int> { room.Game!.CurrentTurn };
        foreach (var (player, i) in new[] { host, second, third, host }.Select((p, i) => (p, i)))
        {
            room.PlayMove(player, MoveIntent.Place(new Position(0, i)), Now.AddSeconds(10 + i), rules);
            turns.Add(room.Game!.CurrentTurn);
        }

        // `generalize-match-seats` 只能用一个假规则证明取模算术,因为 2 号座位没人坐得下。
        // 现在坐得下了 —— 这是**真的三人轮转走过真的聚合**,环走满一圈回到 0。
        turns.Should().Equal(0, 1, 2, 0, 1);
    }

    [Fact]
    public void The_fourth_player_is_refused_at_a_three_seat_table()
    {
        var rules = new SeatsRules(3);
        var room = WaitingRoom(NewUser());
        room.JoinAsPlayer(NewUser(), Now.AddSeconds(1), rules);
        room.JoinAsPlayer(NewUser(), Now.AddSeconds(2), rules);

        var act = () => room.JoinAsPlayer(NewUser(), Now.AddSeconds(3), rules);

        // 满了之后房间已经是 Playing,所以先撞上"不是 Waiting"那条 —— 这也是今天两人棋种的行为。
        act.Should().Throw<RoomNotWaitingException>();
    }

    [Fact]
    public void Sitting_down_twice_is_refused_by_seat_not_by_colour()
    {
        var rules = new SeatsRules(3);
        var host = NewUser();
        var room = WaitingRoom(host);

        var act = () => room.JoinAsPlayer(host, Now.AddSeconds(1), rules);

        act.Should().Throw<AlreadyInRoomException>()
            .WithMessage("*already seated*");
    }

    [Fact]
    public void Swapping_players_swaps_the_first_two_seats()
    {
        var host = NewUser();
        var guest = NewUser();
        var room = WaitingRoom(host);
        room.JoinAsPlayer(guest, Now.AddSeconds(1), BuiltInGameRules.Gomoku);

        room.SwapPlayers(Now.AddSeconds(2));

        room.PlayerAt(0).Should().Be(guest);
        room.PlayerAt(1).Should().Be(host);
        room.SeatOf(guest).Should().Be(0);
        // Host 不变 —— 换的是座位,不是"谁建的房"。
        room.HostUserId.Should().Be(host);
    }

    [Fact]
    public void Seats_are_reported_in_index_order()
    {
        var rules = new SeatsRules(3);
        var room = WaitingRoom(NewUser());
        room.JoinAsPlayer(NewUser(), Now.AddSeconds(1), rules);
        room.JoinAsPlayer(NewUser(), Now.AddSeconds(2), rules);

        // EF 物化时不保证顺序,而 `PlayerAt` / 轮转都按座位号说话 —— 所以顺序由 Seats 保证,
        // 不由加载顺序保证。
        room.Seats.Select(s => s.Index).Should().BeInAscendingOrder();
    }
}
