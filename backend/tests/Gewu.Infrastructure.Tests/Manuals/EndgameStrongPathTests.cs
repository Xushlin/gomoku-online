using System.Text.Json;
using FluentAssertions;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Xiangqi;
using Gewu.Domain.Rooms;
using Gewu.Domain.ValueObjects;
using Gewu.Infrastructure;
using Gewu.Infrastructure.Persistence;

namespace Gewu.Infrastructure.Tests.Manuals;

/// <summary>
/// **「古谱的走法合法性」那笔欠账,现在还欠多少 —— 量出来的,不是推的。**
/// <para>
/// 播种器有两条路:标准开局的 188 条**逐手过 <c>XiangqiRules</c>**,其余 1477 条只做
/// 结构校验(起点有子、双方交替),因为 <c>XiangqiRules</c> 从标准开局重放历史,残局的
/// 第一手在它看来就是非法的。那条弱路径的拆除条件写着「<c>XiangqiRules</c> 能从给定局面
/// 开局的那天」—— 而 <c>play-from-position</c> 就是那天。
/// </para>
/// <para>
/// 所以这里把**全部 1665 条**都走一遍强路径,而结果不是「可以直接换过去」:
/// </para>
/// <list type="bullet">
/// <item><b>1658 条</b>每一半手都合法,而且没有一条在中途就终局;</item>
/// <item><b>7 条被拒</b>,而七条的理由**完全相同**:那一手把自己的将 / 帥送进被将的位置。</item>
/// </list>
/// <para>
/// **这 7 条是数据错,不是规则严。** 逐一看过其中一条:<c>077蛛网空悬</c> 第 3 手走
/// 帥 (9,4) → (9,3),而黑卒就在 (8,3) 上盯着那一格 —— 自杀。而「不许送将」不是一条从
/// 样本里归纳的约定,它是象棋的规则。所以本仓库那条反复付账的教训在这里**指向另一边**:
/// 这一次拒绝是对的,该改的是那 7 条记录。
/// </para>
/// <para>
/// 因此欠账**只还了一半**:能力有了,而换路径之前要先决定那 7 条怎么办(修、丢、还是
/// 单独隔开)。直接换会让播种在第一部谱上抛,而报出来的样子是「产物坏了」。
/// </para>
/// <para>
/// 写成「**恰好** 7 条,而且每一条都是自杀」而不是「至少」:哪天有人修了数据、或者动了
/// 自杀 / 照面那条规则,这条会红,而那正是该重新问「现在能换路径了吗」的时刻。
/// </para>
/// </summary>
public class EndgameStrongPathTests
{
    private static readonly XiangqiEndgameRules Rules = new();

    /// <summary>一条线路走强路径的结果。</summary>
    private sealed record Outcome(string Key, string Title, string? Failure);

    [Fact]
    public void Every_manual_line_but_seven_survives_a_half_move_by_half_move_replay()
    {
        var outcomes = ReplayEverything();

        outcomes.Should().HaveCount(1665, "七部谱一共这么多条 —— 数错了下面全部无意义");

        var failures = outcomes.Where(o => o.Failure is not null).ToList();
        failures.Should().HaveCount(7,
            "1658 条能过强路径。这个数字变了,就该重新问「现在能把播种器换过去了吗」");

        failures.Should().OnlyContain(o => o.Failure!.Contains("check"),
            "七条的理由完全相同:那一手把自己的将送进被将的位置 —— 而那是数据错,不是规则严");

        failures.Select(o => o.Title).Should().BeEquivalentTo(
            [
                "天巧呈能 红胜",
                "玉●无当 红胜",
                "腾天潜渊 红胜",
                "060乌江大战",
                "077蛛网空悬",
                "142声罪致讨",
                "151骑兵破敌",
            ],
            "点名,而不是只数数 —— 换了一条也要红");
    }

    /// <summary>
    /// 反面对照:**这条走查真的在判合法性**。
    /// <para>
    /// 少了它,上面那条在「Apply 从来不抛」时同样是绿的 —— 而那正是这个仓库反复付账的形状。
    /// 判据取一条**确定非法**的走子:把红帥挪到九宫外。
    /// </para>
    /// </summary>
    [Fact]
    public void The_replay_really_rejects_an_illegal_move()
    {
        var cells = new char[XiangqiSetup.BoardLength];
        Array.Fill(cells, '.');
        cells[(0 * 9) + 4] = 'k';
        cells[(9 * 9) + 4] = 'K';
        var setup = new XiangqiSetup(new string(cells), FirstSeat: 0).Encode();

        var act = () => Rules.Apply(
            new MatchState(setup, []),
            MoveIntent.Slide(new Position(9, 4), new Position(9, 0)),
            BoardSeats.FirstSeat);

        act.Should().Throw<Exception>("帥 横走五格出九宫 —— 若这一步都不抛,上面那条走查什么都没验");
    }

    private static List<Outcome> ReplayEverything()
    {
        var results = new List<Outcome>();
        foreach (var key in DependencyInjection.XiangqiManualKeys)
        {
            var path = Path.Combine(AppContext.BaseDirectory, XiangqiManualSeeder.PathFor(key));
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            foreach (var line in doc.RootElement.GetProperty("lines").EnumerateArray())
            {
                results.Add(Replay(key, line));
            }
        }
        return results;
    }

    private static Outcome Replay(string key, JsonElement line)
    {
        var title = line.GetProperty("title").GetString()!;
        var setup = new XiangqiSetup(
            line.GetProperty("start").GetString()!,
            line.GetProperty("firstSeat").GetInt32()).Encode();
        var firstSeat = line.GetProperty("firstSeat").GetInt32();
        var raw = line.GetProperty("moves").GetString()!;

        // 源数据是 (列,行) —— 转置只发生在读进来这一次,与播种器同一条。
        var moves = new List<(Position From, Position To)>();
        for (var i = 0; i + 3 < raw.Length; i += 4)
        {
            moves.Add((new Position(raw[i + 1] - '0', raw[i] - '0'),
                       new Position(raw[i + 3] - '0', raw[i + 2] - '0')));
        }

        var history = new List<PlayedMove>(moves.Count);
        for (var i = 0; i < moves.Count; i++)
        {
            var seat = (firstSeat + i) % 2 == 0 ? BoardSeats.FirstSeat : BoardSeats.SecondSeat;
            MoveApplication applied;
            try
            {
                applied = Rules.Apply(
                    new MatchState(setup, history),
                    MoveIntent.Slide(moves[i].From, moves[i].To), seat);
            }
            catch (Exception ex)
            {
                return new Outcome(key, title, $"ply {i + 1}/{moves.Count}: {ex.Message}");
            }

            if (applied.Result != GameResult.Ongoing && i < moves.Count - 1)
            {
                return new Outcome(key, title,
                    $"ply {i + 1}/{moves.Count} already ends the game ({applied.Result})");
            }
            history.Add(PlayedMove.Positional(moves[i].From, moves[i].To, seat));
        }
        return new Outcome(key, title, null);
    }
}
