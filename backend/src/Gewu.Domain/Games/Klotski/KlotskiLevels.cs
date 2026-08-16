using System.Text.Json;

namespace Gewu.Domain.Games.Klotski;

/// <summary>
/// 关卡工具的公开入口:对一份布局求最优解。
/// <para>
/// 盘面与求解器是 <c>internal</c> 的(它们是规则的内部实现),但**求最优步数**这件事
/// 需要两个外部调用方:生成器要把 <c>minMoves</c> 写进关卡产物,测试要重新算一遍
/// 断言两者一致。给它们一个窄入口,比把整个盘面模型公开出去好。
/// </para>
/// <para>
/// 这一层的存在正是「<c>minMoves</c> 是算出来的,不是抄来的」的落实点 ——
/// 关卡产物里那个数字与测试里那个数字来自同一段代码。
/// </para>
/// </summary>
public static class KlotskiLevels
{
    /// <summary>
    /// 求一份布局的最优解;布局不合法、没有出口、或无解时返回 <c>null</c>。
    /// </summary>
    /// <param name="layoutJson">关卡布局 JSON。</param>
    public static IReadOnlyList<KlotskiMove>? Solve(string layoutJson)
    {
        var layout = Parse(layoutJson);
        if (layout is null)
        {
            return null;
        }

        var (board, exit) = layout.Value;
        return KlotskiSolver.Solve(board, exit.Row, exit.Col);
    }

    /// <summary>最优步数;无解返回 <c>null</c>。</summary>
    /// <param name="layoutJson">关卡布局 JSON。</param>
    public static int? MinMoves(string layoutJson) => Solve(layoutJson)?.Count;

    /// <summary>
    /// 把一串移动重放到布局上,返回末态目标子是否已在出口;任何一步不合法返回 <c>null</c>。
    /// 供测试独立验证一条解,而不必经过 <see cref="KlotskiRules"/>。
    /// </summary>
    /// <param name="layoutJson">关卡布局 JSON。</param>
    /// <param name="moves">要重放的移动。</param>
    public static bool? Replay(string layoutJson, IReadOnlyList<KlotskiMove> moves)
    {
        var layout = Parse(layoutJson);
        if (layout is null)
        {
            return null;
        }

        var (board, exit) = layout.Value;
        foreach (var move in moves)
        {
            var next = board.TryMove(move);
            if (next is null)
            {
                return null;
            }
            board = next;
        }

        return board.Target is { } target && target.Row == exit.Row && target.Col == exit.Col;
    }

    private static (KlotskiBoard Board, KlotskiExit Exit)? Parse(string layoutJson)
    {
        KlotskiLayout? layout;
        try
        {
            layout = JsonSerializer.Deserialize<KlotskiLayout>(layoutJson, KlotskiRules.Json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (layout?.Exit is not { } exit || layout.Pieces is null || layout.Pieces.Count == 0)
        {
            return null;
        }

        var pieces = layout.Pieces
            .Select(p => new KlotskiPiece(p.Id, p.Row, p.Col, p.Height, p.Width, p.Target))
            .ToList();

        var board = KlotskiBoard.TryCreate(layout.Rows, layout.Cols, pieces);
        return board is null ? null : (board, exit);
    }
}
