using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Idioms;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Rooms;

/// <summary>
/// 一局棋可以带一份**服务端侧的对局设置**,而内核从不解释它。
/// <para>
/// 斗地主的发牌是第一个:三家的手牌必须在第一次叫分之前定下来(客户端要看着自己的 17 张牌
/// 决定叫不叫),而它整份 MUST NOT 出服务端。四个现有棋种一样也不需要 —— 它们的开局是常量,
/// 走子历史本来就广播。
/// </para>
/// </summary>
public class GameSetupTests
{
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>不需要设置的探针。</summary>
    private sealed class PlainRules(int seatCount = 2) : IGameRules
    {
        public string GameKey => "plain-probe";
        public int SeatCount { get; } = seatCount;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => false;

        public MoveApplication Apply(
            MatchState state, MoveIntent intent, int seat)
            => MoveApplication.Ongoing();
    }

    /// <summary>需要设置的探针。它**记下自己在 Apply 里看到的设置** —— 那是本组测试的要点之一。</summary>
    private sealed class DealtRules(int seatCount = 2) : IDealtGameRules
    {
        public string GameKey => "dealt-probe";
        public int SeatCount { get; } = seatCount;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => false;

        /// <summary>最近一次 <c>Apply</c> 看到的设置。</summary>
        public string? SeenSetup { get; private set; }

        /// <summary><c>Apply</c> 被调过几次 —— 免得"没看到"与"没被调"混为一谈。</summary>
        public int ApplyCalls { get; private set; }

        public string CreateSetup(int seed) => $"deal-{seed}";

        public MoveApplication Apply(
            MatchState state, MoveIntent intent, int seat)
        {
            ApplyCalls++;
            SeenSetup = state.Setup;
            return MoveApplication.Ongoing();
        }
    }

    private static UserId NewUser() => new(Guid.NewGuid());

    private static Room WaitingRoom(UserId host, string gameKey) =>
        Room.Create(new RoomId(Guid.NewGuid()), "setup", host, Now, gameKey);

    [Fact]
    public void A_game_without_a_setup_stores_null()
    {
        var host = NewUser();
        var room = WaitingRoom(host, "plain-probe");

        room.JoinAsPlayer(NewUser(), Now.AddSeconds(1), new PlainRules(), setup: null);

        // null 而不是 "" —— 空字符串会让"这个棋种没有设置"与"设置是空的"看起来一样。
        room.Game!.Setup.Should().BeNull();
    }

    [Fact]
    public void A_dealt_game_stores_the_setup_verbatim()
    {
        var host = NewUser();
        var room = WaitingRoom(host, "dealt-probe");

        room.JoinAsPlayer(NewUser(), Now.AddSeconds(1), new DealtRules(), setup: "abc");

        room.Game!.Setup.Should().Be("abc", "内核存它、不读它,所以一字不该改");
    }

    [Fact]
    public void A_dealt_game_without_a_setup_is_refused()
    {
        var host = NewUser();
        var room = WaitingRoom(host, "dealt-probe");

        var act = () => room.JoinAsPlayer(
            NewUser(), Now.AddSeconds(1), new DealtRules(), setup: null);

        act.Should().Throw<MissingGameSetupException>()
            .Which.Code.Should().Be("missing-game-setup");

        // 没开局 —— MUST NOT 开出一局没有牌的斗地主。
        room.Status.Should().Be(RoomStatus.Waiting);
        room.Game.Should().BeNull();
    }

    [Fact]
    public void A_plain_game_given_a_setup_is_refused()
    {
        // 第二个方向同样要抛:一个把设置传给不需要设置的棋种的调用方,拿着一个错误的心智模型,
        // 而那份设置会被存下来再也没人读。
        var host = NewUser();
        var room = WaitingRoom(host, "plain-probe");

        var act = () => room.JoinAsPlayer(
            NewUser(), Now.AddSeconds(1), new PlainRules(), setup: "unexpected");

        act.Should().Throw<MissingGameSetupException>();
        room.Status.Should().Be(RoomStatus.Waiting);
    }

    [Fact]
    public void Seating_short_of_a_full_room_does_not_check_the_setup()
    {
        // 一致性校验发生在**开局那一刻**。否则三人棋种的前两次入座都得携带一份最终会被丢掉的
        // 设置,而那份设置的存在会误导下一个读代码的人。
        var rules = new DealtRules(seatCount: 3);
        var host = NewUser();
        var room = WaitingRoom(host, "dealt-probe");

        var act = () => room.JoinAsPlayer(NewUser(), Now.AddSeconds(1), rules, setup: null);

        act.Should().NotThrow();
        room.Status.Should().Be(RoomStatus.Waiting);
        room.Game.Should().BeNull();
    }

    [Fact]
    public void The_setup_arrives_when_the_last_seat_fills()
    {
        var rules = new DealtRules(seatCount: 3);
        var host = NewUser();
        var room = WaitingRoom(host, "dealt-probe");

        room.JoinAsPlayer(NewUser(), Now.AddSeconds(1), rules, setup: null);
        room.JoinAsPlayer(NewUser(), Now.AddSeconds(2), rules, setup: "the-deal");

        room.Status.Should().Be(RoomStatus.Playing);
        room.Game!.Setup.Should().Be("the-deal");
    }

    [Fact]
    public void The_setup_reaches_the_rules()
    {
        // **这条测试是本变更存在的理由。** `add-match-setup` 把设置存进了 `Game`,而
        // `IGameRules.Apply` 的签名是 `(history, intent, seat)` —— 规则拿不到它。一个存下来
        // 再也没人读的值,正是那次变更自己为"不需要设置的棋种却收到设置"加守卫时给出的理由。
        var rules = new DealtRules();
        var host = NewUser();
        var room = WaitingRoom(host, "dealt-probe");
        room.JoinAsPlayer(NewUser(), Now.AddSeconds(1), rules, setup: "the-deal");

        room.PlayMove(host, MoveIntent.Place(new Position(0, 0)), Now.AddSeconds(2), rules);

        rules.ApplyCalls.Should().Be(1);
        rules.SeenSetup.Should().Be("the-deal", "规则读得到开局那份设置,一字不改");
    }

    [Fact]
    public void A_game_without_a_setup_shows_the_rules_null()
    {
        // 对称的一半:不需要设置的棋种在 `Apply` 里看到的是 `null`,而不是空字符串 ——
        // 否则"这个棋种没有设置"与"设置是空的"在规则里同样不可区分。
        var rules = new NullSetupSpy();
        var host = NewUser();
        var room = WaitingRoom(host, "plain-probe");
        room.JoinAsPlayer(NewUser(), Now.AddSeconds(1), rules, setup: null);

        room.PlayMove(host, MoveIntent.Place(new Position(0, 0)), Now.AddSeconds(2), rules);

        rules.ApplyCalls.Should().Be(1);
        rules.SeenSetup.Should().BeNull();
    }

    /// <summary>不需要设置、但记下它看到什么的探针。</summary>
    private sealed class NullSetupSpy : IGameRules
    {
        public string GameKey => "plain-probe";
        public int SeatCount => 2;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => false;

        public string? SeenSetup { get; private set; }

        public int ApplyCalls { get; private set; }

        public MoveApplication Apply(
            MatchState state, MoveIntent intent, int seat)
        {
            ApplyCalls++;
            SeenSetup = state.Setup;
            return MoveApplication.Ongoing();
        }
    }

    [Fact]
    public void CreateSetup_is_a_pure_function_of_its_seed()
    {
        var rules = new DealtRules();

        rules.CreateSetup(20260819).Should().Be(rules.CreateSetup(20260819));
    }

    [Fact]
    public void No_built_in_game_deals_a_setup_yet()
    {
        // 这一条会在斗地主落地那天由那次变更改成"恰好一个实现它"。它现在的价值是钉住
        // **本次变更没有偷偷改动任何现有棋种** —— 那是本变更的验收标准。
        var lexicon = new InMemoryIdiomLexicon(["一心一意"]);

        BuiltInGameRules.All(lexicon).Should().NotContain(r => r is IDealtGameRules);
    }
}
