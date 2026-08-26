using Gewu.Application.Abstractions;
using Gewu.Application.Features.Manuals.GetXiangqiManual;
using Gewu.Application.Features.Manuals.GetXiangqiManualLine;
using Gewu.Domain.Manuals;
using Moq;

namespace Gewu.Application.Tests.Features.Manuals;

/// <summary>
/// 古谱两个查询。
/// <para>
/// 重点是三件**会静默出错**的事:目录的分组必须来自数据(硬编码「8 局」会在下一部谱上
/// 静静对不上)、座位由下标奇偶给出(存一列「谁走的」会是第二份真源)、以及
/// 「不存在」不许长成「空的」。
/// </para>
/// </summary>
public class XiangqiManualQueryHandlerTests
{
    /// <summary>标准开局的盘面串 —— 与播种器里那个常量同源(测试里手写 90 个点不可读)。</summary>
    private const string Standard =
        "rnbakabnr..........c.....c.p.p.p.p.p..................P.P.P.P.P.C.....C..........RNBAKABNR";

    private static string Endgame(params (string Piece, int Row, int Col)[] pieces)
    {
        var b = new char[XiangqiManualLine.BoardStringLength];
        Array.Fill(b, '.');
        foreach (var (piece, row, col) in pieces) b[row * 9 + col] = piece[0];
        return new string(b);
    }

    private static XiangqiManualLine Line(
        int chapter, int order, string title, ManualVerdict verdict, string movesJson,
        string? start = null, int firstSeat = 0)
        => XiangqiManualLine.Create(
            "meihuapu", chapter, order, title, verdict, start ?? Standard, firstSeat, movesJson);

    private static Mock<IXiangqiManualRepository> Repo(params XiangqiManualLine[] lines)
    {
        var repo = new Mock<IXiangqiManualRepository>();
        repo.Setup(r => r.ListLinesAsync("meihuapu", It.IsAny<CancellationToken>()))
            .ReturnsAsync(lines);
        return repo;
    }

    // ---- 目录 ----

    [Fact]
    public async Task Groups_chapters_from_the_data_not_from_a_hardcoded_count()
    {
        var repo = Repo(
            Line(1, 0, "第1局甲", ManualVerdict.RedBetter, "[[9,6,7,4]]"),
            Line(1, 1, "第1局乙", ManualVerdict.BlackBetter, "[[9,6,7,4],[2,7,2,4]]"),
            Line(4, 0, "第4局丙", ManualVerdict.Draw, "[[9,6,7,4]]"));

        var result = await new GetXiangqiManualQueryHandler(repo.Object)
            .Handle(new GetXiangqiManualQuery("meihuapu"), default);

        result.Should().NotBeNull();
        result!.GameKey.Should().Be("xiangqi");
        result.Chapters.Select(c => c.Chapter).Should().Equal([1, 4]);
        result.Chapters[0].Lines.Select(l => l.Title).Should().Equal(["第1局甲", "第1局乙"]);
        result.Chapters[1].Lines.Should().HaveCount(1);
    }

    /// <summary>半手数是算出来的,不是存的一列 —— 所以它不可能与着法漂移。</summary>
    [Fact]
    public async Task Derives_the_move_count_from_the_moves()
    {
        var repo = Repo(Line(1, 0, "第1局甲", ManualVerdict.RedBetter, "[[9,6,7,4],[2,7,2,4],[9,1,7,2]]"));

        var result = await new GetXiangqiManualQueryHandler(repo.Object)
            .Handle(new GetXiangqiManualQuery("meihuapu"), default);

        result!.Chapters[0].Lines[0].MoveCount.Should().Be(3);
    }

    /// <summary>
    /// 子数由**盘面串**算出。它是界面区分残局与满盘的依据,而 MUST NOT 被当成
    /// 「是不是标准开局」—— 实测有 6 局是 32 子却不是标准摆法。
    /// </summary>
    [Fact]
    public async Task Derives_the_piece_count_from_the_start_position()
    {
        var endgame = Endgame(("k", 0, 4), ("K", 9, 4), ("R", 9, 0));
        var repo = Repo(
            Line(0, 0, "残局甲", ManualVerdict.Draw, "[[9,0,8,0]]", endgame),
            Line(0, 1, "满盘乙", ManualVerdict.RedBetter, "[[9,6,7,4]]"));

        var result = await new GetXiangqiManualQueryHandler(repo.Object)
            .Handle(new GetXiangqiManualQuery("meihuapu"), default);

        var counts = result!.Chapters[0].Lines.Select(l => l.PieceCount).ToList();
        counts.Should().Equal([3, 32]);
    }

    /// <summary>先走方是数据 —— 座位从它起交替,而不是从「红先」这条约定起。</summary>
    [Fact]
    public async Task Alternates_seats_from_the_stored_first_seat()
    {
        var endgame = Endgame(("k", 0, 4), ("p", 3, 4), ("K", 9, 4), ("R", 9, 0));
        var line = Line(0, 0, "黑先甲", ManualVerdict.Draw,
            "[[3,4,4,4],[9,0,8,0],[4,4,5,4]]", endgame, firstSeat: 1);
        var repo = new Mock<IXiangqiManualRepository>();
        repo.Setup(r => r.GetLineAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(line);

        var result = await new GetXiangqiManualLineQueryHandler(repo.Object)
            .Handle(new GetXiangqiManualLineQuery(1), default);

        result!.FirstSeat.Should().Be(1);
        result.Moves.Select(m => m.Seat).Should().Equal([1, 0, 1], "黑先,然后交替");
    }

    /// <summary>
    /// 一部谱只因为有线路才存在,所以「零条线路」就是「没有这部谱」。返回一个空目录会让
    /// 打错的键看起来像一部空谱 —— 而那正是这个变更在播种器上修掉过一次的毛病。
    /// </summary>
    [Fact]
    public async Task An_unknown_manual_is_absent_not_empty()
    {
        var repo = new Mock<IXiangqiManualRepository>();
        repo.Setup(r => r.ListLinesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await new GetXiangqiManualQueryHandler(repo.Object)
            .Handle(new GetXiangqiManualQuery("nosuchpu"), default);

        result.Should().BeNull();
    }

    // ---- 单条 ----

    [Fact]
    public async Task Numbers_plies_from_one_and_alternates_seats_starting_with_red()
    {
        var line = Line(2, 1, "第2局甲", ManualVerdict.BlackBetter,
            "[[9,6,7,4],[2,7,2,4],[9,1,7,2],[0,0,1,0]]");
        var repo = new Mock<IXiangqiManualRepository>();
        repo.Setup(r => r.GetLineAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(line);

        var result = await new GetXiangqiManualLineQueryHandler(repo.Object)
            .Handle(new GetXiangqiManualLineQuery(1), default);

        result.Should().NotBeNull();
        result!.Moves.Select(m => m.Ply).Should().Equal([1, 2, 3, 4]);
        result.Moves.Select(m => m.Seat).Should().Equal([0, 1, 0, 1], "红先,座位由下标奇偶给出");
        result.Moves[0].FromRow.Should().Be(9);
        result.Moves[0].FromCol.Should().Be(6);
        result.Moves[0].Row.Should().Be(7);
        result.Moves[0].Col.Should().Be(4);
        result.Chapter.Should().Be(2);
        result.Verdict.Should().Be(ManualVerdict.BlackBetter);
        result.StartPosition.Should().Be(Standard);
        result.FirstSeat.Should().Be(0);
        result.GameKey.Should().Be("xiangqi");
    }

    [Fact]
    public async Task A_missing_line_is_null_so_the_endpoint_can_answer_404()
    {
        var repo = new Mock<IXiangqiManualRepository>();
        repo.Setup(r => r.GetLineAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((XiangqiManualLine?)null);

        var result = await new GetXiangqiManualLineQueryHandler(repo.Object)
            .Handle(new GetXiangqiManualLineQuery(999), default);

        result.Should().BeNull();
    }
}
