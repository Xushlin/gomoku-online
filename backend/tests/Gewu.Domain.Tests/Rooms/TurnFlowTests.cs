using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Idioms;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Rooms;

/// <summary>
/// 「轮到谁、他不走怎么办」—— 内核这两件事都可以由规则决定。
/// <para>
/// 斗地主两件都要:叫分结束之后先出牌的是**地主**(可能是任何一个座位),而超时不能判负
/// (三个座位里"对手"不唯一,"农民赢"也不是一个 <c>WinnerUserId</c> 装得下的结果)。
/// </para>
/// </summary>
public class TurnFlowTests
{
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>座位数与返回值都可配的探针。</summary>
    private class FlowRules(int seatCount, Func<int, MoveApplication>? apply = null) : IGameRules
    {
        public string GameKey => "flow-probe";
        public int SeatCount { get; } = seatCount;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => false;

        public int ApplyCalls { get; private set; }

        public MoveApplication Apply(
            MatchState state, MoveIntent intent, int seat)
        {
            ApplyCalls++;
            return (apply ?? (_ => MoveApplication.Ongoing()))(seat);
        }
    }

    /// <summary>带超时兜底的探针。兜底动作走在一条固定的列上,座位号当行号 —— 每次都是新格子。</summary>
    private sealed class FallbackRules(
        int seatCount,
        Func<int, MoveApplication>? apply = null,
        Func<MatchState, int, MoveIntent>? fallback = null)
        : FlowRules(seatCount, apply), ITimeoutFallbackRules
    {
        public int FallbackCalls { get; private set; }

        public MoveIntent MoveOnTimeout(MatchState state, int seat)
        {
            FallbackCalls++;
            return (fallback ?? ((s, _) => MoveIntent.Place(new Position(s.History.Count, 0))))(state, seat);
        }
    }

    private static UserId NewUser() => new(Guid.NewGuid());

    private static (Room Room, List<UserId> Players) PlayingRoom(IGameRules rules)
    {
        var host = NewUser();
        var room = Room.Create(new RoomId(Guid.NewGuid()), "flow", host, Now, rules.GameKey);
        var players = new List<UserId> { host };
        for (var i = 1; i < rules.SeatCount; i++)
        {
            var next = NewUser();
            room.JoinAsPlayer(next, Now.AddSeconds(i), rules, setup: null);
            players.Add(next);
        }
        return (room, players);
    }

    // ---- 规则决定下一手 -------------------------------------------------

    [Fact]
    public void Without_an_override_the_turn_rotates()
    {
        var rules = new FlowRules(3);
        var (room, players) = PlayingRoom(rules);

        room.PlayMove(players[0], MoveIntent.Place(new Position(0, 0)), Now.AddSeconds(9), rules);

        room.Game!.CurrentTurn.Should().Be(1);
    }

    [Fact]
    public void The_rules_can_name_the_next_seat()
    {
        // 斗地主要的正是这个:叫分结束之后先出牌的是地主,与最后叫分的是谁无关。
        var rules = new FlowRules(3, _ => MoveApplication.OngoingWithTurn(2));
        var (room, players) = PlayingRoom(rules);

        room.PlayMove(players[0], MoveIntent.Place(new Position(0, 0)), Now.AddSeconds(9), rules);

        room.Game!.CurrentTurn.Should().Be(2, "规则说了 2,而轮转会说 1");
    }

    [Fact]
    public void The_override_can_point_back_at_the_same_seat()
    {
        // "同一个人再走一手"是这个字段唯一的其它用法,而它必须表达得出来 —— 否则一个需要
        // 连出两手的规则会被迫在自己内部攒状态,而规则必须无状态。
        var rules = new FlowRules(3, seat => MoveApplication.OngoingWithTurn(seat));
        var (room, players) = PlayingRoom(rules);

        room.PlayMove(players[0], MoveIntent.Place(new Position(0, 0)), Now.AddSeconds(9), rules);

        room.Game!.CurrentTurn.Should().Be(0);
    }

    [Theory]
    [InlineData(GameResult.Decided)]
    [InlineData(GameResult.Draw)]
    public void A_finished_game_cannot_have_a_next_turn(GameResult result)
    {
        var winner = result == GameResult.Decided ? (int?)0 : null;

        var act = () => new MoveApplication(result, winner, NextSeat: 1);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_negative_next_seat_is_not_a_seat()
    {
        var act = () => new MoveApplication(GameResult.Ongoing, null, NextSeat: -1);

        act.Should().Throw<InvalidMoveException>();
    }

    // ---- 超时兜底 -------------------------------------------------------

    [Fact]
    public void Without_a_fallback_a_timeout_still_ends_the_game()
    {
        var rules = new FlowRules(2);
        var (room, players) = PlayingRoom(rules);

        var outcome = room.TimeOutCurrentTurn(Now.AddHours(1), 60, rules);

        outcome.Ended.Should().NotBeNull();
        outcome.Move.Should().BeNull();
        outcome.Ended!.WinnerUserId.Should().Be(players[1]);
        room.Game!.EndReason.Should().Be(GameEndReason.TurnTimeout);
        room.Status.Should().Be(RoomStatus.Finished);
    }

    [Fact]
    public void With_a_fallback_a_timeout_plays_a_move_instead()
    {
        var rules = new FallbackRules(3);
        var (room, _) = PlayingRoom(rules);

        var outcome = room.TimeOutCurrentTurn(Now.AddHours(1), 60, rules);

        outcome.Move.Should().NotBeNull();
        outcome.Ended.Should().BeNull();
        rules.FallbackCalls.Should().Be(1);
        room.Game!.Moves.Should().HaveCount(1);
        room.Game.CurrentTurn.Should().Be(1, "兜底那一步照样推进回合");
        room.Status.Should().Be(RoomStatus.Playing);
    }

    [Fact]
    public void The_fallback_move_goes_through_the_rules()
    {
        // **这是本变更最要紧的一条。** 兜底不是"直接塞一条 Move":它也可能非法(实现出错),
        // 而更要紧的是它可能**结束对局** —— 牌类里替人出掉最后一手牌,那一手就赢了。
        var rules = new FallbackRules(3);
        var (room, _) = PlayingRoom(rules);

        room.TimeOutCurrentTurn(Now.AddHours(1), 60, rules);

        rules.ApplyCalls.Should().Be(1, "Apply 是走子合法性与胜负判定的唯一入口");
    }

    [Fact]
    public void A_fallback_move_that_wins_ends_the_game_as_decided()
    {
        var rules = new FallbackRules(3, _ => MoveApplication.Won(0));
        var (room, players) = PlayingRoom(rules);

        var outcome = room.TimeOutCurrentTurn(Now.AddHours(1), 60, rules);

        outcome.Move.Should().NotBeNull("它仍然是一步棋,只是那一步结束了对局");
        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.WinnerUserId.Should().Be(players[0]);
        room.Game.EndReason.Should().Be(
            GameEndReason.Decided, "它是被规则判出来的,不是超时判的");
    }

    [Fact]
    public void An_illegal_fallback_move_is_refused_and_changes_nothing()
    {
        var rules = new FallbackRules(
            3,
            apply: _ => throw new InvalidMoveException("nope"),
            fallback: (_, _) => MoveIntent.Place(new Position(0, 0)));
        var (room, _) = PlayingRoom(rules);

        var act = () => room.TimeOutCurrentTurn(Now.AddHours(1), 60, rules);

        act.Should().Throw<InvalidMoveException>();
        room.Game!.Moves.Should().BeEmpty();
        room.Game.CurrentTurn.Should().Be(0);
        room.Status.Should().Be(RoomStatus.Playing);
    }

    [Fact]
    public void A_three_seat_game_without_a_fallback_still_refuses()
    {
        // 那条限制没有被放宽,只是有了一个正当的出口。
        var rules = new FlowRules(3);
        var (room, _) = PlayingRoom(rules);

        var act = () => room.TimeOutCurrentTurn(Now.AddHours(1), 60, rules);

        act.Should().Throw<SeatCountNotSupportedException>();
    }

    [Fact]
    public void The_fallback_is_not_consulted_before_the_deadline()
    {
        var rules = new FallbackRules(3);
        var (room, _) = PlayingRoom(rules);

        var act = () => room.TimeOutCurrentTurn(Now.AddSeconds(5), 60, rules);

        act.Should().Throw<TurnNotTimedOutException>();
        rules.FallbackCalls.Should().Be(0, "还没超时就问兜底,等于替一个还在思考的人出手");
    }

    [Fact]
    public void The_fallback_sees_the_setup()
    {
        // **这条测试是本次签名改动的理由。** `MoveOnTimeout` 第一版只收历史,而斗地主首出时的
        // 兜底要出"手上最小的一张单牌" —— 手牌在发牌里,不在历史里。
        //
        // `generalize-turn-flow` 加这个接缝时 `MatchState` 还不存在;紧接着的
        // `pass-setup-to-rules` 为了同一个理由改了 `Apply`,却没回头看几十行之外这个刚加的接缝。
        MatchState? seen = null;
        var rules = new FallbackRules(
            3,
            fallback: (state, _) =>
            {
                seen = state;
                return MoveIntent.Place(new Position(state.History.Count, 0));
            });
        var host = NewUser();
        var room = Room.Create(new RoomId(Guid.NewGuid()), "flow", host, Now, rules.GameKey);
        room.JoinAsPlayer(NewUser(), Now.AddSeconds(1), rules, setup: null);
        room.JoinAsPlayer(NewUser(), Now.AddSeconds(2), rules, setup: null);

        room.TimeOutCurrentTurn(Now.AddHours(1), 60, rules);

        seen.Should().NotBeNull("兜底必须被问到");
        seen!.Value.History.Should().BeEmpty("这一局还没走过任何一步");
    }

    [Fact]
    public void Exactly_one_built_in_game_falls_back_on_timeout()
    {
        // 这一条此前是"还没有棋种实现它",注释里写着斗地主落地那天改成"恰好一个"。照办。
        //
        // **"恰好一个"比"至少一个"有牙**:第二个棋种要兜底的那天它会红,而那正是该问
        // "这两个棋种的超时真是同一种东西吗"的时刻 —— 两个座位下判负仍然是清楚且唯一的答案。
        var lexicon = new InMemoryIdiomLexicon(["一心一意"]);

        BuiltInGameRules.All(lexicon).Where(r => r is ITimeoutFallbackRules)
            .Should().ContainSingle()
            .Which.GameKey.Should().Be(GameKeys.Doudizhu);
    }

    // ---- TurnTimeoutOutcome 的不变量 ------------------------------------

    [Fact]
    public void A_timeout_outcome_carries_exactly_one_half()
    {
        var move = new MoveOutcome(null!, GameResult.Ongoing);
        var ended = new GameEndOutcome(GameResult.Decided, NewUser());

        var both = () => new TurnTimeoutOutcome(move, ended);
        var neither = () => new TurnTimeoutOutcome(null, null);

        both.Should().Throw<InvalidOperationException>();
        neither.Should().Throw<InvalidOperationException>();
    }
}
