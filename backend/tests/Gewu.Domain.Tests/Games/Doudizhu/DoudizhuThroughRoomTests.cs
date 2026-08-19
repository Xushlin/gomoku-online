using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Doudizhu;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.Doudizhu;

/// <summary>
/// 用**真 `Room`** 打一整局斗地主。
/// <para>
/// 这是六个使能变更(`generalize-match-seats`、`add-room-seats`、`generalize-match-outcome`、
/// `add-match-setup`、`generalize-turn-flow`、`pass-setup-to-rules`)的**验收标准**:接缝真的通,
/// 而不是各层单测各自通。与 `XiangqiThroughRoomTests` / `IdiomChainThroughRoomTests` 继承同一条。
/// </para>
/// <para>
/// 它证明的比象棋那一条多:象棋证明了一个 slide 载荷能过聚合,成语接龙证明了一个没有盘面的棋种
/// 能过;这一条是**三个座位、隐藏信息、规则指定下一手**一起过同一个聚合。
/// </para>
/// </summary>
public class DoudizhuThroughRoomTests
{
    private const int Seed = 20260819;

    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DoudizhuRules Rules = new();

    private static (Room Room, List<UserId> Players) PlayingRoom()
    {
        var host = new UserId(Guid.NewGuid());
        var room = Room.Create(
            new RoomId(Guid.NewGuid()), "doudizhu", host, Now, GameKeys.Doudizhu);
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

    [Fact]
    public void A_full_game_runs_through_the_real_aggregate()
    {
        var (room, players) = PlayingRoom();
        var t = 10;

        // 座位 0 叫 3 分 —— 叫分立即结束,他是地主,而**规则把出手权交回给他**
        // (`MoveApplication.NextSeat`)。这是那个字段存在的理由。
        Say(room, players, 0, "bid:3", t++);
        room.Game!.CurrentTurn.Should().Be(0, "地主先出牌,而不是按环轮到 1 号");

        // 地主把 20 张牌一张一张出掉,两名农民每次都过牌。
        //
        // 过牌总是合法(桌上有牌时),所以这个脚本不依赖那副牌里谁能压谁 —— 它只依赖
        // "单牌永远是合法牌型"与"两家过牌之后桌面清空、轮回打出那一手的人"。
        for (var played = 0; played < 20; played++)
        {
            var hand = DoudizhuTable.Reconstruct(room.Game!.State()).HandOf(0);
            hand.Should().HaveCount(20 - played);

            var outcome = Say(room, players, 0, $"play:{hand[0].Encode()}", t++);

            if (played == 19)
            {
                outcome.Result.Should().Be(GameResult.Decided, "最后一张出完就赢了");
                break;
            }

            outcome.Result.Should().Be(GameResult.Ongoing);
            Say(room, players, 1, "pass", t++);
            Say(room, players, 2, "pass", t++);
            room.Game!.CurrentTurn.Should().Be(0, "两家过牌之后轮回打出那一手的人");
        }

        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.WinnerUserId.Should().Be(players[0]);
        room.Game.EndReason.Should().Be(GameEndReason.Decided);
        room.Game.Result.Should().Be(GameResult.Decided);
    }

    [Fact]
    public void Every_move_lands_in_the_history_as_text()
    {
        var (room, players) = PlayingRoom();

        Say(room, players, 0, "bid:3", 10);
        var hand = DoudizhuTable.Reconstruct(room.Game!.State()).HandOf(0);
        Say(room, players, 0, $"play:{hand[0].Encode()}", 11);
        Say(room, players, 1, "pass", 12);

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
    public void Nobody_bidding_draws_through_the_aggregate()
    {
        var (room, players) = PlayingRoom();

        Say(room, players, 0, "bid:0", 10);
        Say(room, players, 1, "bid:0", 11);
        var outcome = Say(room, players, 2, "bid:0", 12);

        outcome.Result.Should().Be(GameResult.Draw);
        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.WinnerUserId.Should().BeNull("流局没有赢家");
    }

    [Fact]
    public void A_timeout_plays_a_move_instead_of_forfeiting()
    {
        // 三个座位里"对手"不唯一,所以超时**不能判负**。兜底替他走一步,对局继续。
        var (room, players) = PlayingRoom();

        var outcome = room.TimeOutCurrentTurn(Now.AddHours(1), 60, Rules);

        outcome.Move.Should().NotBeNull();
        outcome.Ended.Should().BeNull();
        room.Status.Should().Be(RoomStatus.Playing);
        room.Game!.Moves.Should().ContainSingle().Which.Text.Should().Be("bid:0");
    }

    [Fact]
    public void Three_timeouts_during_the_bidding_end_the_game_as_a_draw()
    {
        // 兜底必须**推进**对局。叫分阶段最多三手就结束,而三家都被托管的结果是流局 ——
        // 流局是终局,所以 worker 不会永远在这个房间上打转。
        var (room, players) = PlayingRoom();

        room.TimeOutCurrentTurn(Now.AddHours(1), 60, Rules);
        room.TimeOutCurrentTurn(Now.AddHours(2), 60, Rules);
        var last = room.TimeOutCurrentTurn(Now.AddHours(3), 60, Rules);

        last.Move.Should().NotBeNull("它仍然是一步棋 —— 只是那一步让对局和了");
        room.Status.Should().Be(RoomStatus.Finished);
        room.Game!.Result.Should().Be(GameResult.Draw);
    }

    [Fact]
    public void Not_your_turn_is_still_refused()
    {
        // 内核那三条前置校验(在不在对局、是不是玩家、是不是他的回合)一条都没被绕过。
        var (room, players) = PlayingRoom();

        var act = () => Say(room, players, 1, "bid:1", 10);

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
        // **这条断言的第一版把验收标准写得比这个仓库的实际做法更强,而它自己抓住了我。**
        //
        // 我原本断言"`Rooms/` 与 `Games/Abstractions/` 下都不许提到 Doudizhu",结果它红在
        // `IGameRules.cs` 上 —— 因为 `GameKeys` 就住在那个文件里,而**每一个棋种都往那里加了
        // 一行常量**(gomoku / tictactoe / xiangqi / idiom-chain 全在)。也就是说那条标准与
        // add-xiangqi 以来的每一次都矛盾,只是此前没人把它写成断言。
        //
        // 诚实的标准分两半,下面分别验:聚合(`Rooms/`)一个字都不许提;抽象层只许在
        // `GameKeys` 里出现一个常量。
        var rooms = Directory.GetFiles(
            Path.Combine(SolutionRoot(), "src", "Gewu.Domain", "Rooms"), "*.cs");

        rooms.Should().NotBeEmpty("路径写错了的话下面那条断言会空转通过");

        rooms.Where(p => CodeLines(p).Any(Mentions))
            .Select(Path.GetFileName)
            .Should().BeEmpty("聚合不该认识任何一个具体棋种");
    }

    [Fact]
    public void The_rules_abstractions_only_name_this_game_as_a_key()
    {
        var abstractions = Directory.GetFiles(
            Path.Combine(SolutionRoot(), "src", "Gewu.Domain", "Games", "Abstractions"), "*.cs");

        abstractions.Should().NotBeEmpty();

        var mentions = abstractions.SelectMany(CodeLines).Where(Mentions).Select(l => l.Trim()).ToList();

        // 恰好一行,而且是那个常量声明 —— 与其它四个棋种一模一样的形状。
        mentions.Should().ContainSingle()
            .Which.Should().Be("public const string Doudizhu = \"doudizhu\";");
    }

    private static bool Mentions(string line) =>
        line.Contains("Doudizhu", StringComparison.Ordinal)
        || line.Contains("doudizhu", StringComparison.Ordinal);

    /// <summary>
    /// 剥掉注释行的源码。
    /// <para>
    /// 与 `SeatKernelTests.UsesStone` 同一个做法、同一个理由:第一版连注释一起搜,于是它红在
    /// 我自己写的历史说明上 —— 而那些说明正是要留的东西。
    /// </para>
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
