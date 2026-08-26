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
    [property: JsonPropertyName("firstSeat")] int FirstSeat,
    [property: JsonPropertyName("start")] string Start,
    [property: JsonPropertyName("moves")] string Moves);

/// <summary>产物文件头部:这部谱的身份。</summary>
public sealed record ManualHeader(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("grouped")] bool Grouped);

/// <summary>古谱产物文件。</summary>
public sealed record ManualFile(
    [property: JsonPropertyName("manual")] ManualHeader Manual,
    [property: JsonPropertyName("lines")] IReadOnlyList<ManualLineRecord> Lines);

/// <summary>
/// 把提交进仓库的古谱产物灌入 <c>XiangqiManualLines</c>。**幂等**:该谱已有线路即直接返回。
/// <para>
/// **校验在这里,而不在服务时。** 平台规则是「不可信的输入要过规则」,而华容道之所以在
/// 服务时重放每一个走子,是因为那里有**玩家的声称**要判。古谱没有声称 —— 它是我们自己
/// 装进去的数据,过一次就够;过不去说明数据坏了,不是有人在作弊。
/// </para>
/// <para>
/// **两条校验路径,而非标准开局那条明确更弱 —— 这句话必须写下来。**
/// </para>
/// <list type="bullet">
/// <item><description><b>标准开局</b>:逐手过 <c>XiangqiRules</c>。《梅花谱》31 条走这条,
/// 1391 个半手全部合法;而它同时是坐标解码的证据 —— 转置错了第一手就会被拒。</description></item>
/// <item><description><b>非标准开局</b>(残局,1477 / 1634):**只做结构校验**。
/// <c>XiangqiRules</c> 从标准开局重放历史,残局的第一手在它看来就是非法的,所以那条判据
/// 在这里**用不了**。结构校验 MUST NOT 被说成「校验过走法」。</description></item>
/// </list>
/// <para>
/// 「走法合法」这条判据的**拆除条件是 <c>XiangqiRules</c> 能从给定局面开局** ——
/// 见 <c>CLAUDE.md</c> 的延期表,因为写在规格正文里的触发条件没有人读。
/// </para>
/// </summary>
public sealed class XiangqiManualSeeder
{
    /// <summary>标准开局的盘面串 —— 行优先,红大写黑小写。</summary>
    internal const string StandardBoard =
        "rnbakabnr..........c.....c.p.p.p.p.p..................P.P.P.P.P.C.....C..........RNBAKABNR";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>谱主的评断。未知取值 MUST 抛,而 MUST NOT 默默当成某一方占优。</summary>
    private static readonly IReadOnlyDictionary<string, ManualVerdict> Verdicts =
        new Dictionary<string, ManualVerdict>
        {
            ["red"] = ManualVerdict.RedBetter,
            ["black"] = ManualVerdict.BlackBetter,
            ["draw"] = ManualVerdict.Draw,
            ["unrecorded"] = ManualVerdict.Unrecorded,
        };

    private readonly AppDbContext db;
    private readonly ILogger<XiangqiManualSeeder> logger;
    private readonly string dataPath;

    /// <summary>《梅花谱》产物路径。</summary>
    public static string MeihuapuPath => PathFor("meihuapu");

    /// <summary>某部谱的产物路径。</summary>
    /// <param name="key">古谱键。</param>
    /// <returns>相对 <c>AppContext.BaseDirectory</c> 的路径。</returns>
    public static string PathFor(string key) =>
        Path.Combine("data", "manuals", $"xiangqi-{key}.json");

    /// <summary>构造一个播种器。谱的身份来自**产物文件**,不是构造参数。</summary>
    /// <param name="dataPath">产物文件路径。</param>
    /// <param name="db">数据库上下文。</param>
    /// <param name="logger">日志。</param>
    public XiangqiManualSeeder(string dataPath, AppDbContext db, ILogger<XiangqiManualSeeder> logger)
    {
        this.dataPath = dataPath;
        this.db = db;
        this.logger = logger;
    }

    /// <summary>灌入线路。已有即跳过。</summary>
    /// <param name="ct">取消标记。</param>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // 相对路径按 AppContext.BaseDirectory 解析 —— 与 PuzzleLevelSeeder 同一条,
        // 因为进程的工作目录不是输出目录。
        var resolved = Path.IsPathRooted(dataPath)
            ? dataPath
            : Path.Combine(AppContext.BaseDirectory, dataPath);

        // **缺文件抛,不是 warn。** 量过一次:warn + return 的结果是端点返回 200 加一个
        // 空目录 —— 一次静默的空导入,而它和成功导入在接口上长得一模一样。
        if (!File.Exists(resolved))
        {
            throw new FileNotFoundException(
                $"Manual data file not found at {resolved}. It is committed under " +
                "backend/data/manuals/ and copied to the output by Gewu.Infrastructure.csproj; a " +
                "missing copy rule would otherwise show up as an empty catalogue with a 200.",
                resolved);
        }

        var text = await File.ReadAllTextAsync(resolved, ct);
        var file = JsonSerializer.Deserialize<ManualFile>(text, JsonOptions)
            ?? throw new InvalidDataException($"Manual file {resolved} did not parse.");
        var key = file.Manual?.Key
            ?? throw new InvalidDataException($"Manual file {resolved} has no manual.key.");

        if (await db.XiangqiManualLines.AnyAsync(l => l.ManualKey == key, ct))
        {
            logger.LogInformation("Manual {Key} already seeded; skipping.", key);
            return;
        }
        if (file.Lines.Count == 0)
        {
            throw new InvalidDataException($"Manual {key} has no lines — refusing an empty import.");
        }

        var rules = (IBoardGameRules)BuiltInGameRules.Xiangqi;
        var perChapter = new Dictionary<int, int>();
        var lines = new List<XiangqiManualLine>(file.Lines.Count);
        var plies = 0;
        var strict = 0;

        foreach (var record in file.Lines)
        {
            // 分组:有「第N局」那一层的谱从标题里推局号;没有那一层的一律 0,
            // 而 MUST NOT 为了形状一致给残局编一个局号 —— 那是编数据。
            var chapter = file.Manual.Grouped ? ParseChapter(record.Title) : 0;
            if (!Verdicts.TryGetValue(record.Verdict ?? string.Empty, out var verdict))
            {
                throw new InvalidDataException(
                    $"{record.Title}: unknown verdict {record.Verdict}.");
            }

            var start = record.Start ?? string.Empty;
            if (start.Length != XiangqiManualLine.BoardStringLength)
            {
                throw new InvalidDataException(
                    $"{record.Title}: start position is {start.Length} chars, expected " +
                    $"{XiangqiManualLine.BoardStringLength}.");
            }

            var standard = start == StandardBoard;
            var moves = standard
                ? ReplayThroughRules(rules, record)
                : CheckStructurally(record, start);
            if (standard) strict++;
            plies += moves.Count;

            perChapter.TryGetValue(chapter, out var order);
            perChapter[chapter] = order + 1;

            lines.Add(XiangqiManualLine.Create(
                key,
                chapter,
                order,
                record.Title,
                verdict,
                start,
                record.FirstSeat,
                JsonSerializer.Serialize(moves)));
        }

        if (!await db.XiangqiManuals.AnyAsync(m => m.Key == key, ct))
        {
            db.XiangqiManuals.Add(XiangqiManual.Create(key, file.Manual.Name, file.Manual.Grouped));
        }
        db.XiangqiManualLines.AddRange(lines);
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Seeded manual {Key} ({Name}): {Lines} lines, {Plies} half-moves; " +
            "{Strict} replayed through {Game} rules, {Loose} structurally checked.",
            key, file.Manual.Name, lines.Count, plies, strict, rules.GameKey,
            lines.Count - strict);
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
                $"{title}: cannot read a chapter number — a grouped manual's title must start " +
                "with the chapter marker.");
        }
        var digits = title[1..end];
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var chapter)
            || chapter <= 0)
        {
            throw new InvalidDataException($"{title}: {digits} is not a chapter number.");
        }
        return chapter;
    }

    /// <summary>四位一组拆成本项目坐标。来源是「列在前」,转置只发生在这里。</summary>
    private static List<int[]> Decode(ManualLineRecord record)
    {
        var raw = record.Moves ?? string.Empty;
        if (raw.Length == 0 || raw.Length % 4 != 0)
        {
            throw new InvalidDataException(
                $"{record.Title}: move string length {raw.Length} is not a positive multiple of 4.");
        }

        var moves = new List<int[]>(raw.Length / 4);
        for (var i = 0; i < raw.Length / 4; i++)
        {
            var g = raw.Substring(i * 4, 4);
            foreach (var c in g)
            {
                if (c is < '0' or > '9')
                {
                    throw new InvalidDataException(
                        $"{record.Title}: half-move {i + 1} ({g}) is not four digits.");
                }
            }
            // (列,行) -> Position(行, 列)
            moves.Add([g[1] - '0', g[0] - '0', g[3] - '0', g[2] - '0']);
        }
        return moves;
    }

    /// <summary>
    /// **标准开局那条路径:逐手过规则。**
    /// <para>
    /// 终局的两条判据方向相反,而两条都是量出来的:最后一手**可以**不判终局 ——
    /// 《梅花谱》31 条里 20 条走到「优势已成」就停,一条「末手必须终局」的校验会把它们全部
    /// 拒掉,而报出来的样子和「数据坏了」一模一样;反过来,**中途**判终局是坏数据
    /// (实测 0 例),它最可能说明坐标解码错了。
    /// </para>
    /// </summary>
    internal static List<int[]> ReplayThroughRules(IBoardGameRules rules, ManualLineRecord record)
    {
        var moves = Decode(record);
        var history = new List<PlayedMove>(moves.Count);

        for (var i = 0; i < moves.Count; i++)
        {
            var m = moves[i];
            var from = new Position(m[0], m[1]);
            var to = new Position(m[2], m[3]);
            var seat = (record.FirstSeat + i) % 2 == 0 ? BoardSeats.FirstSeat : BoardSeats.SecondSeat;

            MoveApplication applied;
            try
            {
                applied = rules.Apply(new MatchState(null, history), MoveIntent.Slide(from, to), seat);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"{record.Title}: half-move {i + 1}/{moves.Count} seat={seat} was rejected " +
                    $"by the rules: {ex.Message}", ex);
            }

            if (applied.Result != GameResult.Ongoing && i < moves.Count - 1)
            {
                throw new InvalidDataException(
                    $"{record.Title}: half-move {i + 1}/{moves.Count} already ends the game " +
                    $"({applied.Result}) but the line continues — most likely the coordinates are " +
                    "being decoded wrongly, not that the manual is wrong.");
            }

            history.Add(PlayedMove.Positional(from, to, seat));
        }

        return moves;
    }

    /// <summary>
    /// **残局那条路径:只做结构校验,而这明确比过规则弱。**
    /// <para>
    /// 推演由一个**只搬子、不判合法性**的循环完成 —— 它 MUST NOT 被当成规则引擎的一部分。
    /// 能查的是:每一手的起点在当前推演出来的局面上真的有子、终点在盘内、以及**双方交替**
    /// (从存下来的先走方起)。**查不了的是这一步走法本身合不合规。**
    /// </para>
    /// <para>
    /// 交替是这里唯一能抓到「某一方连走两手」的东西,而 1634 局残局里 7 局是黑先走 ——
    /// 所以起点是 <c>FirstSeat</c>,而 MUST NOT 假设红先。
    /// </para>
    /// </summary>
    internal static List<int[]> CheckStructurally(ManualLineRecord record, string start)
    {
        var moves = Decode(record);
        var board = start.ToCharArray();

        for (var i = 0; i < moves.Count; i++)
        {
            var m = moves[i];
            foreach (var v in m)
            {
                if (v is < 0 or > 9)
                {
                    throw new InvalidDataException(
                        $"{record.Title}: half-move {i + 1} has coordinate {v} outside 0..9.");
                }
            }
            if (m[1] > 8 || m[3] > 8)
            {
                throw new InvalidDataException(
                    $"{record.Title}: half-move {i + 1} has a column outside 0..8.");
            }

            var fromIdx = m[0] * 9 + m[1];
            var toIdx = m[2] * 9 + m[3];
            var piece = board[fromIdx];
            if (piece == '.')
            {
                throw new InvalidDataException(
                    $"{record.Title}: half-move {i + 1}/{moves.Count} starts from an empty square " +
                    $"(row {m[0]}, col {m[1]}).");
            }

            var moverSeat = char.IsUpper(piece) ? BoardSeats.FirstSeat : BoardSeats.SecondSeat;
            var expected = (record.FirstSeat + i) % 2 == 0
                ? BoardSeats.FirstSeat
                : BoardSeats.SecondSeat;
            if (moverSeat != expected)
            {
                throw new InvalidDataException(
                    $"{record.Title}: half-move {i + 1}/{moves.Count} moves {piece} (seat " +
                    $"{moverSeat}) but the alternation from firstSeat={record.FirstSeat} expects " +
                    $"seat {expected}.");
            }

            board[fromIdx] = '.';
            board[toIdx] = piece;
        }

        return moves;
    }
}
