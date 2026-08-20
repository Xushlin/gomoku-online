using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Idioms;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Rooms;

/// <summary>
/// 谁先走,可以由规则决定。
/// <para>
/// 内核的默认是 0 号座位,而那对到目前为止的每一个棋种都成立 —— 五个棋种的先手都是**约定**
/// (谁坐 0 号谁先)。挖坑不是:持最小 ♣ 的人首叫且首出,而那是**发牌**决定的。
/// 把发牌旋转成"最小 ♣ 总在 0 号"在统计上等价、在体验上不等价:那样同一个人每一局都先叫。
/// </para>
/// <para>
/// **两个方向都要钉住**,这是 `generalize-turn-flow` 给 <c>NextSeat</c> 留下的教训:一个带默认
/// 含义的东西,只钉一边会让"默认被当成必选"悄悄通过。
/// </para>
/// </summary>
public class FirstSeatTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>不管首手的探针 —— 五个现有棋种都是这一类。</summary>
    private class PlainRules(int seatCount = 3) : IGameRules
    {
        public string GameKey => "plain-probe";
        public int SeatCount { get; } = seatCount;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => false;

        public MoveApplication Apply(MatchState state, MoveIntent intent, int seat)
            => MoveApplication.Ongoing();
    }

    /// <summary>指定首手的探针。它**记下自己看到的 state** —— 首手座位来自发牌,所以它必须看得到设置。</summary>
    private sealed class FirstSeatRules(int seat, int seatCount = 3)
        : PlainRules(seatCount), IFirstSeatRules
    {
        public MatchState? Seen { get; private set; }

        public int FirstSeat(MatchState state)
        {
            Seen = state;
            return seat;
        }
    }

    /// <summary>既发牌又指定首手 —— 挖坑就是这一类。</summary>
    private sealed class DealtFirstSeatRules(int seat)
        : PlainRules(3), IDealtGameRules, IFirstSeatRules
    {
        public string CreateSetup(int seed) => $"deal-{seed}";

        public int FirstSeat(MatchState state) => seat;
    }

    private static UserId NewUser() => new(Guid.NewGuid());

    private static Room Seated(IGameRules rules, string? setup = null)
    {
        var room = Room.Create(new RoomId(Guid.NewGuid()), "first-seat", NewUser(), Now, rules.GameKey);
        for (var i = 1; i < rules.SeatCount; i++)
        {
            room.JoinAsPlayer(NewUser(), Now.AddSeconds(i), rules, setup);
        }
        return room;
    }

    [Fact]
    public void Without_the_seam_the_game_starts_at_seat_zero()
    {
        var room = Seated(new PlainRules());

        room.Game!.CurrentTurn.Should().Be(0, "默认没有变,五个现有棋种一行不动");
    }

    [Fact]
    public void The_rules_can_name_another_seat()
    {
        var room = Seated(new FirstSeatRules(seat: 2));

        room.Game!.CurrentTurn.Should().Be(2);
    }

    [Fact]
    public void The_rules_see_the_setup_when_they_pick_the_first_seat()
    {
        // 挖坑的首手**是发牌算出来的**,所以这条不是"顺便传了个参数",而是这个 seam 的全部用处。
        var rules = new DealtFirstSeatRules(seat: 1);
        var room = Seated(rules, setup: "deal-42");

        room.Game!.CurrentTurn.Should().Be(1);
        room.Game!.Setup.Should().Be("deal-42");
    }

    [Fact]
    public void The_history_is_empty_at_kickoff()
    {
        // 开局那一刻还没有任何一手 —— 规则若去读历史,读到的必须是空的,而不是 null。
        var rules = new FirstSeatRules(seat: 1);
        _ = Seated(rules);

        rules.Seen.Should().NotBeNull();
        rules.Seen!.Value.History.Should().BeEmpty();
        rules.Seen!.Value.Setup.Should().BeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void A_seat_outside_the_table_is_refused_at_kickoff(int seat)
    {
        // 存下来会造出一局**谁都动不了**的棋:每个人都不是当前回合,于是几十秒后由超时兜底
        // 暴露出来 —— 而那时报的是超时,不是"首手座位是 99"。
        var act = () => Seated(new FirstSeatRules(seat));

        act.Should().Throw<InvalidFirstSeatException>()
            .Which.Code.Should().Be("invalid-first-seat");
    }

    [Fact]
    public void A_refused_first_seat_leaves_the_room_unstarted()
    {
        var rules = new FirstSeatRules(seat: 7);
        var room = Room.Create(new RoomId(Guid.NewGuid()), "first-seat", NewUser(), Now, rules.GameKey);
        room.JoinAsPlayer(NewUser(), Now.AddSeconds(1), rules, setup: null);

        var act = () => room.JoinAsPlayer(NewUser(), Now.AddSeconds(2), rules, setup: null);

        act.Should().Throw<InvalidFirstSeatException>();
        room.Status.Should().Be(RoomStatus.Waiting, "MUST NOT 开出一局谁都动不了的棋");
        room.Game.Should().BeNull();
    }

    [Fact]
    public void No_built_in_game_picks_its_first_seat_yet()
    {
        // **挖坑落地那天这条会红,那时把它改成"恰好一个"** —— 与
        // `Exactly_one_built_in_game_deals_a_setup` 走过的同一条路(那一条也是从
        // "还没有棋种实现它"改过来的)。「恰好一个」比「至少一个」有牙:第二个出现时它会红,
        // 而那正是该问"这两个棋种的先手真是同一种东西吗"的时刻。
        var lexicon = new InMemoryIdiomLexicon(["一心一意"]);

        BuiltInGameRules.All(lexicon).Where(r => r is IFirstSeatRules)
            .Should().BeEmpty();
    }

    [Fact]
    public void Every_built_in_game_still_starts_at_seat_zero()
    {
        // 上一条是"没有人实现这个接口";这一条是"于是每一局都从 0 号开始"。
        // 两条都要有:接口没被实现,和默认没被改坏,是两件事。
        var lexicon = new InMemoryIdiomLexicon(["一心一意"]);

        foreach (var rules in BuiltInGameRules.All(lexicon))
        {
            var setup = rules is IDealtGameRules dealt ? dealt.CreateSetup(20260820) : null;
            var room = Seated(rules, setup);

            room.Game!.CurrentTurn.Should().Be(
                0, $"'{rules.GameKey}' 的先手仍然是约定,不是规则");
        }
    }
}
