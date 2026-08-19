using System;
using System.Collections.Generic;
using FluentAssertions;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Rooms;

/// <summary>
/// "谁赢了"这个事实只有**一个**住处。
/// <para>
/// 此前它有两个:<c>Game.Result</c> 的取值(<c>BlackWin</c> / <c>WhiteWin</c>)与
/// <c>Game.WinnerUserId</c>。镜像是第二份真源,而两份真源不一致的那天不会有东西报错。
/// 顺带,一个带颜色的取值只够表示两个座位 —— 三座位棋种的 2 号赢了根本没有值可以写。
/// </para>
/// </summary>
public class RoomOutcomeTests
{
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>座位数可配、结果可配的探针规则。</summary>
    private sealed class OutcomeRules(int seatCount, MoveApplication? application = null) : IGameRules
    {
        public string GameKey => "outcome-probe";
        public int SeatCount { get; } = seatCount;
        public bool SupportsHumanVsHuman => true;

        // 三座位而计分会违反 `IsRated ⇒ SeatCount == 2`,所以探针不计分。
        public bool IsRated => false;

        public MoveApplication Apply(
            IReadOnlyList<PlayedMove> history, MoveIntent intent, int seat)
            => application ?? MoveApplication.Ongoing();
    }

    private static UserId NewUser() => new(Guid.NewGuid());

    private static (Room Room, List<UserId> Players) PlayingRoom(
        int seatCount, MoveApplication? application = null)
    {
        var rules = new OutcomeRules(seatCount, application);
        var host = NewUser();
        var room = Room.Create(new RoomId(Guid.NewGuid()), "outcome", host, Now, "outcome-probe");
        var players = new List<UserId> { host };
        for (var i = 1; i < seatCount; i++)
        {
            var next = NewUser();
            room.JoinAsPlayer(next, Now.AddSeconds(i), rules, setup: null);
            players.Add(next);
        }
        return (room, players);
    }

    [Fact]
    public void The_winner_is_looked_up_by_seat()
    {
        var (room, players) = PlayingRoom(2, MoveApplication.Won(1));

        room.PlayMove(
            players[0], MoveIntent.Place(new Position(0, 0)), Now.AddSeconds(9),
            new OutcomeRules(2, MoveApplication.Won(1)));

        room.Game!.Result.Should().Be(GameResult.Decided);
        room.Game.WinnerUserId.Should().Be(players[1]);
    }

    [Fact]
    public void A_third_seat_can_win()
    {
        // **这一条只有三座位才证得到。** 旧形状下 2 号座位赢了没有值可以表示 ——
        // `BlackWin` / `WhiteWin` 是一个封了顶的枚举,而这个洞是"先问这个值从哪来"
        // 才发现的:它一直是 `WinnerUserId` 的副本。
        var rules = new OutcomeRules(3, MoveApplication.Won(2));
        var (room, players) = PlayingRoom(3, MoveApplication.Won(2));

        room.PlayMove(players[0], MoveIntent.Place(new Position(0, 0)), Now.AddSeconds(9), rules);

        room.Game!.Result.Should().Be(GameResult.Decided);
        room.Game.WinnerUserId.Should().Be(players[2], "赢家从 PlayerAt(2) 查出来");
    }

    [Fact]
    public void A_draw_has_no_winner()
    {
        var rules = new OutcomeRules(2, MoveApplication.Drawn());
        var (room, players) = PlayingRoom(2, MoveApplication.Drawn());

        room.PlayMove(players[0], MoveIntent.Place(new Position(0, 0)), Now.AddSeconds(9), rules);

        room.Game!.Result.Should().Be(GameResult.Draw);
        room.Game.WinnerUserId.Should().BeNull();
    }

    [Fact]
    public void Resigning_needs_exactly_two_seats()
    {
        // 三座位下"对手"不唯一:斗地主里农民认输,赢的是地主还是另一个农民,是那个棋种的规则问题。
        //
        // **拒绝而不是猜。** 旧代码在这里的行为是三个静默的错答案:0 号认输判 1 号胜(2 号不在
        // 话下),2 号认输得到 `NotAPlayerException`。今天没有三座位棋种,所以这条抛不出来;
        // 它存在是为了让第一个三座位棋种**必须**先回答这个问题。
        var (room, players) = PlayingRoom(3);

        var act = () => room.Resign(players[1], Now.AddSeconds(9));

        act.Should().Throw<SeatCountNotSupportedException>()
            .Which.Code.Should().Be("seat-count-not-supported");
    }

    [Fact]
    public void Timing_out_needs_exactly_two_seats()
    {
        // 同上,而这一条更要紧:`TurnTimeoutWorker` 会周期性调它。一个每次都抛的调用点
        // 就是 `enforce-ai-availability` 那个缺陷的形状 —— worker 每 1500 ms 抛进日志的虚空,
        // 房间永远停在那里。所以三座位棋种落地**之前**必须先给出它的超时语义。
        var (room, _) = PlayingRoom(3);

        var act = () => room.TimeOutCurrentTurn(Now.AddHours(1), 60);

        act.Should().Throw<SeatCountNotSupportedException>();
    }

    [Fact]
    public void Resigning_a_two_seat_game_still_names_the_opponent()
    {
        var (room, players) = PlayingRoom(2);

        var outcome = room.Resign(players[0], Now.AddSeconds(9));

        outcome.Result.Should().Be(GameResult.Decided);
        outcome.WinnerUserId.Should().Be(players[1]);
        room.Game!.WinnerUserId.Should().Be(players[1]);
    }

    [Theory]
    [InlineData(GameResult.Ongoing)]
    [InlineData(GameResult.Draw)]
    public void A_result_without_a_winner_must_not_carry_one(GameResult result)
    {
        var act = () => new MoveApplication(result, 0);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_decided_result_must_carry_a_winner()
    {
        var act = () => new MoveApplication(GameResult.Decided, null);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void A_negative_seat_is_not_a_seat()
    {
        var act = () => new MoveApplication(GameResult.Decided, -1);

        act.Should().Throw<InvalidMoveException>();
    }

    [Fact]
    public void The_three_factories_produce_the_only_legal_combinations()
    {
        // 工厂是约定,构造器是机制 —— 上面四条钉的是机制。这一条钉的是三个工厂确实落在
        // 机制允许的那三种组合上,免得工厂自己写错却没人发现。
        MoveApplication.Ongoing().Should().Be(new MoveApplication(GameResult.Ongoing, null));
        MoveApplication.Drawn().Should().Be(new MoveApplication(GameResult.Draw, null));
        MoveApplication.Won(2).Should().Be(new MoveApplication(GameResult.Decided, 2));
    }
}
