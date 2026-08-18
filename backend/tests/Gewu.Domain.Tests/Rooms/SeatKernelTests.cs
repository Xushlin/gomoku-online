using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Enums;
using Gewu.Domain.Games;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Rooms;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Rooms;

/// <summary>
/// 内核说座位号,不说棋色(<c>generalize-match-seats</c>)。
/// </summary>
public class SeatKernelTests
{
    private static readonly System.DateTime Now =
        new(2026, 8, 18, 12, 0, 0, System.DateTimeKind.Utc);

    /// <summary>座位数可配的探针规则。它永远判 <c>Ongoing</c> —— 本文件只关心轮转。</summary>
    private sealed class SeatsRules(int seatCount) : IGameRules
    {
        public string GameKey => "seats-probe";

        public int SeatCount { get; } = seatCount;

        public bool SupportsHumanVsHuman => true;

        // 三座位而计分会违反不变量二,所以探针不计分。
        public bool IsRated => false;

        public MoveApplication Apply(
            IReadOnlyList<PlayedMove> history, MoveIntent intent, int seat)
            => new(GameResult.Ongoing);
    }

    private static Room PlayingRoom()
    {
        var host = new Domain.Users.UserId(System.Guid.NewGuid());
        var guest = new Domain.Users.UserId(System.Guid.NewGuid());
        var room = Room.Create(new RoomId(System.Guid.NewGuid()), "seats", host, Now, "seats-probe");
        room.JoinAsPlayer(guest, Now.AddSeconds(1));
        return room;
    }

    [Fact]
    public void A_two_seat_game_alternates_exactly_as_before()
    {
        var room = PlayingRoom();
        var rules = new SeatsRules(2);
        var seats = new List<int> { room.Game!.CurrentTurn };

        room.PlayMove(room.BlackPlayerId, MoveIntent.Place(new Position(0, 0)), Now.AddSeconds(2), rules);
        seats.Add(room.Game!.CurrentTurn);
        room.PlayMove(room.WhitePlayerId!.Value, MoveIntent.Place(new Position(0, 1)), Now.AddSeconds(3), rules);
        seats.Add(room.Game!.CurrentTurn);
        room.PlayMove(room.BlackPlayerId, MoveIntent.Place(new Position(0, 2)), Now.AddSeconds(4), rules);
        seats.Add(room.Game!.CurrentTurn);

        // 与改动前的 Black → White → Black → White 逐步等价 —— 这次改动行为零变化。
        seats.Should().Equal(0, 1, 0, 1);
    }

    [Fact]
    public void A_three_seat_game_walks_the_ring_instead_of_flipping()
    {
        var room = PlayingRoom();
        var rules = new SeatsRules(3);

        room.Game!.CurrentTurn.Should().Be(0);
        room.PlayMove(room.BlackPlayerId, MoveIntent.Place(new Position(0, 0)), Now.AddSeconds(2), rules);
        room.Game!.CurrentTurn.Should().Be(1);
        room.PlayMove(room.WhitePlayerId!.Value, MoveIntent.Place(new Position(0, 1)), Now.AddSeconds(3), rules);

        // 改动前这里会翻回 0,因为那一行是 `stone == Black ? White : Black`。
        // 现在是 (1 + 1) % 3 == 2。
        room.Game!.CurrentTurn.Should().Be(2);

        // 而 2 号座位现在**没有人坐** —— 房间只有两个座位字段。这正是下一个变更
        // (`add-room-seats`)存在的原因,写在这里而不是留一句 TODO。
        room.SeatOf(room.BlackPlayerId).Should().Be(0);
        room.SeatOf(room.WhitePlayerId!.Value).Should().Be(1);
    }

    [Fact]
    public void What_this_proves_is_modulo_arithmetic_and_not_that_the_seam_fits_cards()
    {
        // 上面那条用的是一个假的三座位规则。一个 fake 证明不了**接缝的形状** ——
        // `add-puzzle-core` 注册过一个照着唯一实现捏的 fake 来"证明"接缝通用,
        // 华容道一到,`Validate` 与 `Score` 两个都得改。
        //
        // 它能证明的是 `(seat + 1) % n`,因为被测的东西就是那个算术。区别在于:被测的是
        // "这个接口对第二种实现够不够用",还是"这段算式对不对"。
        var rules = new SeatsRules(3);

        rules.SeatCount.Should().Be(3);
        Enumerable.Range(0, 7).Select(i => i % rules.SeatCount)
            .Should().Equal(0, 1, 2, 0, 1, 2, 0);
    }

    [Fact]
    public void Every_registered_game_has_two_seats_today()
    {
        // 本次改动行为零变化,这条是它的可执行形式。第一个三座位棋种落地那天,
        // 这条会红 —— 那时它该被改成"每个计分棋种两个座位"。
        foreach (var rules in BuiltInGameRules.All(new NoIdioms()))
        {
            rules.SeatCount.Should().Be(2, $"{rules.GameKey} is a two-seat game today");
        }
    }

    [Fact]
    public void A_rated_game_must_have_exactly_two_seats()
    {
        // 不变量二,遍历注册表强制。现有 ELO 是两人制的。
        foreach (var rules in BuiltInGameRules.All(new NoIdioms()))
        {
            if (rules.IsRated)
            {
                rules.SeatCount.Should().Be(2, $"{rules.GameKey} is rated, and ELO is a two-player rating");
            }
        }
    }

    [Fact]
    public void The_kernel_source_does_not_mention_Stone()
    {
        // 「内核不知道一个棋种有几个人」的可执行形式,与 `in-room-chat` 那条
        // 「JoinAsSpectator 不许提到 GameKey」同一种断言。
        var kernel = Path.Combine(SolutionRoot(), "src", "Gewu.Domain", "Rooms");
        var offenders = Directory.EnumerateFiles(kernel, "*.cs", SearchOption.AllDirectories)
            .Where(UsesStone)
            .Select(Path.GetFileName)
            .ToList();

        offenders.Should().BeEmpty(
            "Stone belongs to the board family; the match kernel speaks seat numbers");
    }

    /// <summary>
    /// 去掉注释之后的源码。
    /// <para>
    /// 断言的是「内核不**用** <c>Stone</c>」,不是「这个词不许出现」—— `Game` 与 `Move` 上都留着
    /// 一句"此前这里是 Stone"的说明,而那正是要留的东西。第一版这条测试连注释一起搜,
    /// 于是它红了,红在我自己写的历史说明上。
    /// </para>
    /// <para>
    /// 按行剥 <c>//</c> / <c>///</c> / 块注释行,是个近似:字符串字面量里的 <c>Stone</c> 仍会被抓到。
    /// 那正合意 —— 内核里出现一个叫 Stone 的字面量,和用这个类型一样是回退。
    /// </para>
    /// </summary>
    private static bool UsesStone(string path) =>
        File.ReadAllLines(path)
            .Where(l =>
            {
                var t = l.TrimStart();
                return !t.StartsWith("//") && !t.StartsWith("*") && !t.StartsWith("/*");
            })
            .Any(l => l.Contains("Stone"));

    /// <summary>不带词典的 lexicon —— 本文件只走注册表,不真的判成语。</summary>
    private sealed class NoIdioms : Domain.Idioms.IIdiomLexicon
    {
        public bool Contains(string word) => false;
    }

    /// <summary>从测试程序集向上找到解决方案根 —— 源码断言要读文件。</summary>
    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gewu.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new System.InvalidOperationException("Gewu.slnx not found above the test binaries.");
    }
}
