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
    public void Exactly_two_built_in_games_pick_their_first_seat()
    {
        // 这一条走过三代:先是"还没有棋种实现它",挖坑落地那天改成"恰好一个",而注释里
        // 写着「第二个出现时它会红,那正是该问"这两个棋种的先手真是同一种东西吗"的时刻」。
        //
        // **第二个出现了,而问题的答案是:是同一种东西。**
        //
        //   挖坑  —— 先手由**发牌**决定(拿到某张牌的那个人先叫);
        //   残局  —— 先手由**谱**决定(1634 局里 7 局是黑先走)。
        //
        // 两者都是「**服务端侧的设置**说了算」,而那正是这个接口承载的东西 —— 不同的只是
        // 设置从哪来(规则摇出来 / 调用方选出来),而那件事由 IDealtGameRules 与
        // IPositionalStartRules 分别承载。所以接口不用拆,名单加一个。
        var lexicon = new InMemoryIdiomLexicon(["一心一意"]);

        BuiltInGameRules.All(lexicon).Where(r => r is IFirstSeatRules)
            .Select(r => r.GameKey)
            .Should().BeEquivalentTo([GameKeys.Wakeng, GameKeys.XiangqiEndgame]);
    }

    /// <summary>
    /// 两种**设置来源**都要在注册表里有样本。
    /// <para>
    /// 少了任何一边,`Room` 里那条「是不是这两者之一」的判断就只走过一条腿 —— 而它在
    /// 单一种类上恒真。
    /// </para>
    /// </summary>
    [Fact]
    public void Both_setup_sources_have_a_built_in_example()
    {
        var lexicon = new InMemoryIdiomLexicon(["一心一意"]);
        var all = BuiltInGameRules.All(lexicon);

        all.Where(r => r is IDealtGameRules).Select(r => r.GameKey)
            .Should().BeEquivalentTo([GameKeys.Doudizhu, GameKeys.Wakeng], "规则从种子生成");
        all.Where(r => r is IPositionalStartRules).Select(r => r.GameKey)
            .Should().BeEquivalentTo([GameKeys.XiangqiEndgame], "调用方选定、规则校验");

        // 而**没有棋种同时是两者** —— 一份设置只能有一个来源,两个来源会让"谁负责它的内容"
        // 没有答案。
        all.Where(r => r is IDealtGameRules and IPositionalStartRules).Should().BeEmpty();
    }

    /// <summary>
    /// 从选定局面开局的棋种 MUST NOT 计分。
    /// <para>
    /// 与既有的 <c>IsRated ⇒ SeatCount == 2</c> 并列。理由是残局**按构造就不公平** ——
    /// 有一方是赢定的,那是谱主设计它的方式;给这样的局面算 ELO 是在给一个已知结局的
    /// 局面发分。
    /// </para>
    /// </summary>
    [Fact]
    public void A_game_that_starts_from_a_chosen_position_is_never_rated()
    {
        var lexicon = new InMemoryIdiomLexicon(["一心一意"]);

        BuiltInGameRules.All(lexicon)
            .Where(r => r is IPositionalStartRules && r.IsRated)
            .Select(r => r.GameKey)
            .Should().BeEmpty();
    }

    [Fact]
    public void Every_game_without_the_seam_still_starts_at_seat_zero()
    {
        // 上一条是"恰好一个棋种实现这个接口";这一条是"于是其余每一个都还从 0 号开始"。
        // 两条都要有:接口被谁实现了,和默认有没有被改坏,是两件事。
        //
        // 它此前遍历**全部**棋种并要求每一个都是 0 —— 挖坑落地那天它当然会红,而红法很有用:
        // 它报的是 `found 1`,也就是挖坑在这个种子下的首叫者是 1 号而不是 0 号。
        // 下面 `The_first_seat_of_wakeng_is_its_own_first_bidder` 用的就是那个事实。
        var lexicon = new InMemoryIdiomLexicon(["一心一意"]);
        var walked = 0;

        foreach (var rules in BuiltInGameRules.All(lexicon).Where(r => r is not IFirstSeatRules))
        {
            var setup = rules is IDealtGameRules dealt ? dealt.CreateSetup(20260820) : null;
            var room = Seated(rules, setup);

            room.Game!.CurrentTurn.Should().Be(
                0, $"'{rules.GameKey}' 的先手仍然是约定,不是规则");
            walked++;
        }

        walked.Should().BeGreaterThan(1, "遍历若走空,这条断言什么都没验");
    }

    [Fact]
    public void The_first_seat_of_wakeng_is_its_own_first_bidder()
    {
        // 上面那条遍历**不覆盖**实现了 seam 的棋种,所以这一条单独钉它 —— 一条只走"其余棋种"
        // 的遍历,在挖坑的 `FirstSeat` 被改成 `=> 0` 之后照样全绿。
        //
        // 这个种子下首叫者是 **1 号**,而不是 0 号 —— 那不是巧合,是这条断言的**前提**:
        // 若首叫者恰好是 0 号,"轮到首叫者"与"轮到 0 号"在同一个断言下不可区分,
        // 而一个忽略发牌的实现会因为别的理由通过。
        var rules = new Gewu.Domain.Games.Wakeng.WakengRules();
        var setup = rules.CreateSetup(20260820);
        var expected = Gewu.Domain.Games.Wakeng.WakengDeal.Decode(setup).FirstBidder().Seat;

        expected.Should().NotBe(0, "这个种子的首叫者必须不是 0 号,否则下一条断言证明不了什么");
        Seated(rules, setup).Game!.CurrentTurn.Should().Be(expected);
    }
}
