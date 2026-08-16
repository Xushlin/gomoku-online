using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gewu.Domain.Games.Klotski;

namespace Gewu.Tools.KlotskiGenerator;

/// <summary>产物里的一关。<c>Layout</c> / <c>Solution</c> 与 seeder 读的形状一致。</summary>
/// <param name="LevelIndex">关卡序号。</param>
/// <param name="Difficulty">难度分档。</param>
/// <param name="Layout">布局。</param>
/// <param name="Solution">答案(只有计分参数)。</param>
public sealed record LevelRecord(
    [property: JsonPropertyName("levelIndex")] int LevelIndex,
    [property: JsonPropertyName("difficulty")] int Difficulty,
    [property: JsonPropertyName("layout")] JsonElement Layout,
    [property: JsonPropertyName("solution")] JsonElement Solution);

/// <summary>产物文件。</summary>
/// <param name="Game">游戏键。</param>
/// <param name="Levels">关卡。</param>
public sealed record LevelFile(
    [property: JsonPropertyName("game")] string Game,
    [property: JsonPropertyName("levels")] IReadOnlyList<LevelRecord> Levels);

/// <summary>关卡设计:一个人写的布局 + 一句它是什么。</summary>
/// <param name="Name">显示名。</param>
/// <param name="Difficulty">难度分档,1 起。</param>
/// <param name="Pieces">棋子。</param>
internal sealed record Design(string Name, int Difficulty, IReadOnlyList<PieceSpec> Pieces);

/// <summary>布局里的一枚子,含只给客户端看的显示名。</summary>
/// <param name="Id">标识。</param>
/// <param name="Name">显示名(领域层忽略)。</param>
/// <param name="Row">左上角行。</param>
/// <param name="Col">左上角列。</param>
/// <param name="Height">占几行。</param>
/// <param name="Width">占几列。</param>
/// <param name="Target">是否是要送出去的那一枚。</param>
internal sealed record PieceSpec(
    string Id, string Name, int Row, int Col, int Height, int Width, bool Target = false);

/// <summary>
/// 生成 <c>backend/data/levels/klotski.json</c>。
/// <para>
/// 布局是**手写**的 —— 华容道的经典局面是文化物,不是随机产物。工具只做一件事:
/// 对每份布局跑求解器,把最优步数记进产物。**布局由人给,难度由算法测。**
/// </para>
/// <para>
/// 因此本工具不引用任何出版物上的步数。经典局面的公开数字随数法而异(连滑算一步 vs
/// 一格一步),抄进来既不可复现又可能不自洽。
/// </para>
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        // 显式给 LF:.NET 9 起 WriteIndented 默认用 Environment.NewLine,于是同一份
        // 布局在 Windows 和 Linux 上会生成不同的字节 —— 而这份产物是**提交进仓库**的,
        // 它的内容还会原样进数据库。让它与平台无关。
        NewLine = "\n",
    };

    /// <summary>盘面是 5×4,曹操要把左上角送到第 3 行第 1 列(底部正中)。</summary>
    private const int Rows = 5;
    private const int Cols = 4;
    private const int ExitRow = 3;
    private const int ExitCol = 1;

    private static int Main(string[] args)
    {
        var output = args.Length > 0
            ? args[0]
            : Path.Combine("data", "levels", "klotski.json");

        var levels = new List<LevelRecord>();

        for (var i = 0; i < Designs.Count; i++)
        {
            var design = Designs[i];
            var layoutJson = JsonSerializer.Serialize(ToLayout(design), Json);

            var started = Stopwatch.StartNew();
            var solution = KlotskiLevels.Solve(layoutJson);
            started.Stop();

            if (solution is null)
            {
                Console.Error.WriteLine($"[{design.Name}] 无解 —— 布局写错了,产物不写。");
                return 1;
            }

            Console.WriteLine(
                $"[{i}] {design.Name,-12} 难度 {design.Difficulty}  " +
                $"minMoves = {solution.Count,3}  ({started.ElapsedMilliseconds} ms)");

            levels.Add(new LevelRecord(
                i,
                design.Difficulty,
                JsonSerializer.Deserialize<JsonElement>(layoutJson),
                JsonSerializer.SerializeToElement(new KlotskiSolution(solution.Count), Json)));
        }

        var file = new LevelFile("klotski", levels);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(file, Json) + "\n");

        Console.WriteLine($"写入 {output}({levels.Count} 关)。");
        return 0;
    }

    private static object ToLayout(Design design) => new
    {
        rows = Rows,
        cols = Cols,
        name = design.Name,
        exit = new { row = ExitRow, col = ExitCol },
        pieces = design.Pieces.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            row = p.Row,
            col = p.Col,
            height = p.Height,
            width = p.Width,
            target = p.Target,
        }),
    };

    // ---- 手写布局 ----
    //
    // 全部以「横刀立马」为基准:曹操 2×2 居上正中,四将竖立两侧,关羽横卧其下,
    // 四卒填底。前四关是它**去掉若干枚子**得到的 —— 少一枚子严格意味着更多空格、
    // 更少约束,所以这个派生方向本身就保证了难度单调下降。这一点是可说清的,
    // 而「我记得某个名字对应某个摆法」不是,所以只有最后一关声称自己是经典局面。

    private static PieceSpec CaoCao() => new("cao", "曹操", 0, 1, 2, 2, Target: true);

    private static PieceSpec[] Generals() =>
    [
        new("zhang", "张飞", 0, 0, 2, 1),
        new("ma", "马超", 0, 3, 2, 1),
        new("zhao", "赵云", 2, 0, 2, 1),
        new("huang", "黄忠", 2, 3, 2, 1),
    ];

    private static PieceSpec GuanYu(int row) => new("guan", "关羽", row, 1, 1, 2);

    private static PieceSpec Soldier(string id, int row, int col) => new(id, "卒", row, col, 1, 1);

    /// <summary>横刀立马 —— 经典局面。</summary>
    private static IReadOnlyList<PieceSpec> HengDaoLiMa() =>
    [
        CaoCao(),
        .. Generals(),
        GuanYu(2),
        Soldier("s1", 3, 1),
        Soldier("s2", 3, 2),
        Soldier("s3", 4, 0),
        Soldier("s4", 4, 3),
    ];

    private static IReadOnlyList<PieceSpec> Without(params string[] ids)
        => [.. HengDaoLiMa().Where(p => !ids.Contains(p.Id))];

    private static readonly IReadOnlyList<Design> Designs =
    [
        new("初识华容", 1, Without("s1", "s2", "s3", "s4")),
        new("四卒当关", 1, Without("s1", "s2")),
        new("兵临城下", 2, Without("s3", "s4")),
        new("一卒之差", 2, Without("s1")),
        new("横刀立马", 3, HengDaoLiMa()),
    ];
}
