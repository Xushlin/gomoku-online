using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Manuals;
using Gewu.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gewu.Infrastructure.Persistence;

/// <summary>古谱产物里的一条线路。<c>Moves</c> 是来源格式,4 位一组的「列行列行」。</summary>
public sealed record ManualLineRecord(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("verdict")] string Verdict,
    [property: JsonPropertyName("moves")] string Moves);

/// <summary>古谱产物文件。<c>StartPosition</c> 是全文件共用的起始局面(来源格式)。</summary>
public sealed record ManualFile(
    [property: JsonPropertyName("startPosition")] string StartPosition,
    [property: JsonPropertyName("lines")] IReadOnlyList<ManualLineRecord> Lines);

/// <summary>
/// 把提交进仓库的古谱产物灌入 <c>XiangqiManualLines</c>。**幂等**:该谱已有线路即直接返回。
/// <para>
/// **校验在这里,而不在服务时。** 平台规则是「不可信的输入要过规则」,而华容道之所以在
/// 服务时重放每一个走子,是因为那里有**玩家的声称**要判。古谱没有声称 —— 它是我们自己
/// 装进去的数据,过一次就够;过不去说明数据坏了,不是有人在作弊。
/// </para>
/// <para>
/// 它同时是**坐标解码的唯一证据**:来源的坐标是「列在前」而本项目是「行在前」,
/// 转置错了的话第一手就会被规则拒掉。所以这里 MUST NOT 有「跳过不认识的着法」这种
/// 分支 —— 那会把解码错误变成一次静默的空导入,而空导入和成功导入打印一样的东西。
/// </para>
/// </summary>
public sealed class XiangqiManualSeeder
{
    /// <summary>标准开局摆法,来源格式(32 个棋子,每个两位数「列行」)。</summary>
    internal const string StandardStart =
        "8979695949392919097717866646260600102030405060708012720323436383";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>谱主的评断到座位号。未知取值 MUST 抛,而不是默默当成红方。</summary>
    private static readonly IReadOnlyDictionary<string, int> Verdicts = new Dictionary<string, int>
    {
        ["red"] = BoardSeats.FirstSeat,
        ["black"] = BoardSeats.SecondSeat,
    };

    private readonly AppDbContext db;
    private readonly ILogger<XiangqiManualSeeder> logger;
    private readonly string manualKey;
    private readonly string dataPath;

    /// <summary>《梅花谱》产物路径。</summary>
    public static string MeihuapuPath => Path.Combine("data", "manuals", "xiangqi-meihuapu.json");

    /// <summary>构造一个播种器 —— 参数顺序与 <see cref="PuzzleLevelSeeder"/> 一致。</summary>
    /// <param name="manualKey">古谱键,例如 meihuapu。</param>
    /// <param name="dataPath">产物文件路径。</param>
    /// <param name="db">数据库上下文。</param>
    /// <param name="logger">日志。</param>
    public XiangqiManualSeeder(
        string manualKey,
        string dataPath,
        AppDbContext db,
        ILogger<XiangqiManualSeeder> logger)
    {
        this.db = db;
        this.logger = logger;
        this.manualKey = manualKey;
        this.dataPath = dataPath;
    }

    /// <summary>灌入线路。已有即跳过。</summary>
    /// <param name="ct">取消标记。</param>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.XiangqiManualLines.AnyAsync(l => l.ManualKey == manualKey, ct))
        {
            logger.LogInformation("Manual {Key} already seeded; skipping.", manualKey);
            return;
        }
        // 相对路径按 AppContext.BaseDirectory 解析 —— 与 PuzzleLevelSeeder 同一条,
        // 因为进程的工作目录不是输出目录。
        var resolved = Path.IsPathRooted(dataPath)
            ? dataPath
            : Path.Combine(AppContext.BaseDirectory, dataPath);

        // **缺文件抛,不是 warn。** 量过一次:warn + return 的结果是端点返回 200 加一个
        // 空目录 —— 一次静默的空导入,而它和成功导入在接口上长得一模一样。产物是提交进
        // 仓库的,它缺席只意味着构建的复制规则漏了,而那要在启动时就炸。
        if (!File.Exists(resolved))
        {
            throw new FileNotFoundException(
                $"Manual data file for {manualKey} not found at {resolved}. It is committed under " +
                "backend/data/manuals/ and copied to the output by Gewu.Infrastructure.csproj; a " +
                "missing copy rule would otherwise show up as an empty catalogue with a 200.",
                resolved);
        }

        var text = await File.ReadAllTextAsync(resolved, ct);
        var file = JsonSerializer.Deserialize<ManualFile>(text, JsonOptions)
            ?? throw new InvalidDataException($"Manual file {resolved} did not parse.");

        if (file.StartPosition != StandardStart)
        {
            throw new InvalidDataException(
                $"Manual {manualKey} starts from a non-standard position. Replaying it from the " +
                "standard opening would silently validate a different game; set-up positions are " +
                "a separate feature.");
        }
        if (file.Lines.Count == 0)
        {
            throw new InvalidDataException(
                $"Manual {manualKey} has no lines — refusing an empty import.");
        }

        var rules = (IBoardGameRules)BuiltInGameRules.Xiangqi;
        var perChapter = new Dictionary<int, int>();
        var lines = new List<XiangqiManualLine>(file.Lines.Count);
        var totalPlies = 0;

        foreach (var record in file.Lines)
        {
            var chapter = ParseChapter(record.Title);
            if (!Verdicts.TryGetValue(record.Verdict, out var winnerSeat))
            {
                throw new InvalidDataException(
                    $"{record.Title}: unknown verdict {record.Verdict}.");
            }

            var moves = DecodeAndValidate(rules, record);
            totalPlies += moves.Count;

            perChapter.TryGetValue(chapter, out var order);
            perChapter[chapter] = order + 1;

            lines.Add(XiangqiManualLine.Create(
                manualKey,
                chapter,
                order,
                record.Title,
                winnerSeat,
                JsonSerializer.Serialize(moves)));
        }

        db.XiangqiManualLines.AddRange(lines);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seeded manual {Key}: {Lines} lines, {Plies} half-moves, all legal under {Game} rules.",
            manualKey, lines.Count, totalPlies, rules.GameKey);
    }

    /// <summary>从原书局名里取出局号。取不出 MUST 抛 —— 目录的分组靠它。</summary>
    /// <param name="title">原书局名,形如「第1局取中兵压马破上右士」。</param>
    /// <returns>局号,1 起。</returns>
    internal static int ParseChapter(string title)
    {
        var end = title.IndexOf('局');
        if (title.Length == 0 || title[0] != '第' || end <= 1)
        {
            throw new InvalidDataException(
                $"{title}: cannot read a chapter number — a title must start with the chapter marker.");
        }
        var digits = title[1..end];
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var chapter)
            || chapter <= 0)
        {
            throw new InvalidDataException($"{title}: {digits} is not a chapter number.");
        }
        return chapter;
    }

    /// <summary>
    /// 把来源格式的着法串转置成本项目坐标,并**逐手过规则**。
    /// <para>
    /// 终局的两条判据方向相反,而两条都是量出来的:最后一手**可以**不判终局 —— 31 条
    /// 线路里 20 条走到「优势已成」就停,一条「末手必须终局」的校验会把它们全部拒掉,
    /// 而报出来的样子和「数据坏了」一模一样;反过来,**中途**判终局是坏数据(实测 0 例),
    /// 它最可能说明坐标解码错了。
    /// </para>
    /// </summary>
    /// <param name="rules">象棋规则。</param>
    /// <param name="record">一条线路。</param>
    /// <returns>本项目坐标下的着法,每项四个数:起点行列、终点行列。</returns>
    internal static List<int[]> DecodeAndValidate(IBoardGameRules rules, ManualLineRecord record)
    {
        var raw = record.Moves ?? string.Empty;
        if (raw.Length == 0 || raw.Length % 4 != 0)
        {
            throw new InvalidDataException(
                $"{record.Title}: move string length {raw.Length} is not a positive multiple of 4.");
        }

        var plies = raw.Length / 4;
        var history = new List<PlayedMove>(plies);
        var moves = new List<int[]>(plies);

        for (var i = 0; i < plies; i++)
        {
            var group = raw.Substring(i * 4, 4);
            if (!TryDigit(group[0], out var fromCol) || !TryDigit(group[1], out var fromRow)
                || !TryDigit(group[2], out var toCol) || !TryDigit(group[3], out var toRow))
            {
                throw new InvalidDataException(
                    $"{record.Title}: half-move {i + 1} ({group}) is not four digits.");
            }

            // 来源是「列,行」;本项目是 Position(行, 列)。转置只发生在这里。
            var from = new Position(fromRow, fromCol);
            var to = new Position(toRow, toCol);
            var seat = i % 2 == 0 ? BoardSeats.FirstSeat : BoardSeats.SecondSeat;

            MoveApplication applied;
            try
            {
                applied = rules.Apply(new MatchState(null, history), MoveIntent.Slide(from, to), seat);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"{record.Title}: half-move {i + 1}/{plies} ({group}) seat={seat} was rejected " +
                    $"by the rules: {ex.Message}", ex);
            }

            if (applied.Result != GameResult.Ongoing && i < plies - 1)
            {
                throw new InvalidDataException(
                    $"{record.Title}: half-move {i + 1}/{plies} ({group}) already ends the game " +
                    $"({applied.Result}) but the line continues — most likely the coordinates are " +
                    "being decoded wrongly, not that the manual is wrong.");
            }

            history.Add(PlayedMove.Positional(from, to, seat));
            moves.Add([from.Row, from.Col, to.Row, to.Col]);
        }

        return moves;
    }

    private static bool TryDigit(char c, out int value)
    {
        value = c - '0';
        return c is >= '0' and <= '9';
    }
}
