using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Wakeng;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.Wakeng;

/// <summary>
/// 用**真 `Room`** 打一整局挖坑。
/// <para>
/// 这是 `generalize-match-kickoff` 的**验收标准**:那个 seam 真的通,而不是各层单测各自通。
/// 与 `DoudizhuThroughRoomTests` / `XiangqiThroughRoomTests` / `IdiomChainThroughRoomTests`
/// 继承同一条。
/// </para>
/// <para>
/// 它证明的比斗地主那一条多一件事:**先手不再是「谁坐 0 号」**。斗地主证明了三个座位、
/// 隐藏信息、规则指名下一手能过同一个聚合;这一条加上「开局那一刻由发牌决定谁先动」。
/// </para>
/// </summary>
public class WakengThroughRoomTests
{
    private const int Seed = 20260820;

    private static readonly DateTime Now = new(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc);

    private static readonly WakengRules Rules = new();

    private static (Room Room, List<UserId> Players) PlayingRoom()
    {
        var host = new UserId(Guid.NewGuid());
        var room = Room.Create(new RoomId(Guid.NewGuid()), "wakeng", host, Now, GameKeys.Wakeng);
        var players = new List<UserId> { host };

        // 三个座位:第二个人坐进来时房间**留在 Waiting**,第三个人坐满才开局。
        var second = new UserId(Guid.NewGuid());
        room.JoinAsPlayer(second, Now.AddSeconds(1), Rules, setup: null);
        players.Add(second);
        room.Status.Should().Be(RoomStatus.Waiting, "三座位棋种坐两个人还不开局");

        var third = new UserId(Guid.NewGuid());
        room.JoinAsPlayer(third, Now.AddSeconds(2), Rules, setup: Rules.CreateSetup(Seed));
        players.Add(third);
        room.Status.Should().Be(RoomStatus.Playing);

        return (room, players);
    }

    private static MoveOutcome Say(Room room, List<UserId> players, int seat, string text, int at)
        => room.PlayMove(players[seat], MoveIntent.Say(text), Now.AddSeconds(at), Rules);

    private static int FirstBidder(Room room)
        => WakengTable.Reconstruct(room.Game!.State()).FirstBidderSeat;

    [Fact]
    public void The_game_opens_on_the_seat_the_deal_chose()
    {
        var (room, _) = PlayingRoom();

        var first = FirstBidder(room);

        room.Game!.CurrentTurn.Should().Be(first);
        // **这个种子下首叫者不是 0 号,而那是这条断言的前提** —— 若它恰好是 0,
        // 「轮到首叫者」与内核的默认值不可区分,一个忽略发牌的实现会因为别的理由通过。
        first.Should().NotBe(0, "这个种子的首叫者必须不是 0 号,否则上面那条断言证明不了什么");
    }

    [Fact]
    public void A_full_game_runs_through_the_real_aggregate()
    {
        var (room, players) = PlayingRoom();
        var first = FirstBidder(room);
        var t = 10;

        // 首叫者叫 3 分 —— 叫分立即结束,他是挖坑者,而出手权在他手里(他本来就首出)。
        Say(room, players, first, "bid:3", t++);
        room.Game!.CurrentTurn.Should().Be(first);

        var digger = WakengTable.Reconstruct(room.Game!.State()).Digger;
        digger.Should().Be(first);

        // 挖坑者把 20 张牌一张一张出掉,另两家每次都过牌。
        //
        // 过牌总是合法(桌上有牌时),所以这个脚本不依赖那副牌里谁能压谁 —— 它只依赖
        // 「单牌永远是合法牌型」与「两家过牌之后桌面清空、轮回打出那一手的人」。
        var total = WakengDeal.HandSize + WakengDeal.KittySize;
        for (var played = 0; played < total; played++)
        {
            var hand = WakengTable.Reconstruct(room.Game!.State()).HandOf(first);
            hand.Should().HaveCount(total - played);

            var outcome = Say(room, players, first, $"play:{hand[0].Encode()}", t++);

            if (played == total - 1)
            {
                outcome.Result.Should().Be(GameResult.Decided, "最后一张出完就赢了");
                break;
            }

            outcome.Result.Should().Be(GameResult.Ongoing);
            Say(room, players, (first + 1) % 3, "pass", t++);
            Say(room, players, (first + 2) % 3, "pass", t++);
            room.Game!.CurrentTurn.Should().Be(first, "两家过牌之后轮回打出那一手的人");
        }

        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.WinnerUserId.Should().Be(players[first]);
        room.Game.EndReason.Should().Be(GameEndReason.Decided);
        room.Game.Result.Should().Be(GameResult.Decided);
    }

    [Fact]
    public void Every_move_lands_in_the_history_as_text()
    {
        var (room, players) = PlayingRoom();
        var first = FirstBidder(room);

        Say(room, players, first, "bid:3", 10);
        var hand = WakengTable.Reconstruct(room.Game!.State()).HandOf(first);
        Say(room, players, first, $"play:{hand[0].Encode()}", 11);
        Say(room, players, (first + 1) % 3, "pass", 12);

        room.Game!.Moves.Should().HaveCount(3);
        foreach (var move in room.Game.Moves)
        {
            move.Text.Should().NotBeNullOrWhiteSpace();
            move.Row.Should().BeNull();
            move.Col.Should().BeNull();
            move.FromRow.Should().BeNull();
            move.FromCol.Should().BeNull();
        }
    }

    [Fact]
    public void Nobody_digging_does_not_draw_the_game()
    {
        // 斗地主在这条路径上是**流局**;挖坑不是 —— 首叫者兜底 1 倍,对局继续。
        var (room, players) = PlayingRoom();
        var first = FirstBidder(room);

        Say(room, players, first, "bid:0", 10);
        Say(room, players, (first + 1) % 3, "bid:0", 11);
        var outcome = Say(room, players, (first + 2) % 3, "bid:0", 12);

        outcome.Result.Should().Be(GameResult.Ongoing);
        outcome.Result.Should().NotBe(GameResult.Draw);
        room.Status.Should().Be(RoomStatus.Playing);
        room.Game!.CurrentTurn.Should().Be(first);

        var table = WakengTable.Reconstruct(room.Game!.State());
        table.Digger.Should().Be(first);
        table.Bid.Should().Be(WakengScoring.ForcedBid);
    }

    [Fact]
    public void A_timeout_plays_a_move_instead_of_forfeiting()
    {
        // 三个座位里「对手」不唯一,所以超时**不能判负**。兜底替他走一步,对局继续。
        var (room, _) = PlayingRoom();

        var outcome = room.TimeOutCurrentTurn(Now.AddHours(1), 60, Rules);

        outcome.Move.Should().NotBeNull();
        outcome.Ended.Should().BeNull();
        room.Status.Should().Be(RoomStatus.Playing);
        room.Game!.Moves.Should().ContainSingle().Which.Text.Should().Be("bid:0");
    }

    [Fact]
    public void Timeouts_alone_reach_the_end_of_the_game()
    {
        // **这是「兜底 MUST 推进对局」的可执行形式,而不是一段论证。**
        //
        // 挖坑的终止论证与斗地主不同:那边三家都不叫就流局,三步终局;这边三家都不挖会
        // **进入出牌阶段并继续**,所以推进靠的是每一次首出都让一张牌离开某只手。
        //
        // 上限存在是为了让「不推进」表现成**跑不完**,而不是表现成挂住。
        // 算一下:3 次叫分 + 20 张牌 × (1 次首出 + 2 次过牌) = 63 步。
        var (room, _) = PlayingRoom();
        const int Cap = 200;
        var steps = 0;

        while (room.Status == RoomStatus.Playing && steps < Cap)
        {
            room.TimeOutCurrentTurn(Now.AddHours(1 + steps), 60, Rules);
            steps++;
        }

        room.Status.Should().Be(RoomStatus.Finished, $"{Cap} 步之内 MUST 结束");
        steps.Should().BeLessThan(Cap);
        room.Game!.Result.Should().Be(GameResult.Decided, "有人出完了牌 —— 挖坑不会和");
    }

    [Fact]
    public void Not_your_turn_is_still_refused()
    {
        // 内核那三条前置校验(在不在对局、是不是玩家、是不是他的回合)一条都没被绕过。
        var (room, players) = PlayingRoom();
        var notFirst = (FirstBidder(room) + 1) % 3;

        var act = () => Say(room, players, notFirst, "bid:1", 10);

        act.Should().Throw<Gewu.Domain.Exceptions.NotYourTurnException>();
    }

    [Fact]
    public void The_deal_never_leaves_the_server()
    {
        // `Game.Setup` 就是三家的底牌。它 MUST NOT 出现在任何 DTO 上 —— 那条反射断言在
        // Gewu.Application.Tests 里。这一条钉的是它**确实存在于聚合上**,即那条断言守的东西非空。
        var (room, _) = PlayingRoom();

        room.Game!.Setup.Should().NotBeNullOrEmpty();
        room.Game.Setup.Should().Be(Rules.CreateSetup(Seed));
    }

    [Fact]
    public void The_match_aggregate_does_not_know_this_game_exists()
    {
        // 验收标准的第一半。`add-doudizhu` 立下的形状:聚合一个字都不许提。
        var rooms = Directory.GetFiles(
            Path.Combine(SolutionRoot(), "src", "Gewu.Domain", "Rooms"), "*.cs");

        rooms.Should().NotBeEmpty("路径写错的话下面那条断言会空转通过");

        rooms.Where(p => CodeLines(p).Any(Mentions))
            .Select(Path.GetFileName)
            .Should().BeEmpty("聚合不该认识任何一个具体棋种");
    }

    [Fact]
    public void The_rules_abstractions_only_name_this_game_as_a_key()
    {
        // 验收标准的第二半。抽象层只许出现一行 —— `GameKeys` 里那个常量,与另外五个棋种
        // 一模一样的形状。一条「抽象层完全不提」的标准会与 add-xiangqi 以来的每一次矛盾。
        var abstractions = Directory.GetFiles(
            Path.Combine(SolutionRoot(), "src", "Gewu.Domain", "Games", "Abstractions"), "*.cs");

        abstractions.Should().NotBeEmpty();

        var mentions = abstractions.SelectMany(CodeLines).Where(Mentions).Select(l => l.Trim()).ToList();

        mentions.Should().ContainSingle()
            .Which.Should().Be("public const string Wakeng = \"wakeng\";");
    }

    private static bool Mentions(string line) =>
        line.Contains("Wakeng", StringComparison.Ordinal)
        || line.Contains("wakeng", StringComparison.Ordinal);

    /// <summary>
    /// 剥掉注释行的源码 —— 与 `DoudizhuThroughRoomTests` / `SeatKernelTests` 同一个做法、
    /// 同一个理由:连注释一起搜,会红在那些**要留下来的**历史说明上。
    /// </summary>
    private static IEnumerable<string> CodeLines(string path) =>
        File.ReadAllLines(path).Where(l =>
        {
            var t = l.TrimStart();
            return !t.StartsWith("//", StringComparison.Ordinal)
                && !t.StartsWith("*", StringComparison.Ordinal)
                && !t.StartsWith("/*", StringComparison.Ordinal);
        });

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gewu.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new InvalidOperationException("Gewu.slnx not found above the test binaries.");
    }
}
