using System.Text.Encodings.Web;
using System.Text.Json;
using Gewu.Domain.Puzzles;

namespace Gewu.Domain.Games.Klotski;

/// <summary>
/// 华容道的规则 —— 平台的第二个 <see cref="IPuzzleRules"/> 实现,也是第一个答案是
/// **一条路径**而不是**一份填空**的谜题。
/// <para>
/// <b>它的权威来自重放,不来自隐藏。</b> 成语纵横的服务端权威建立在「答案不下发」上;
/// 华容道什么都不藏 —— 棋子、盘面、出口、滑动规则全部公开且全部在客户端,因为一个
/// 判不了滑动的客户端连动画都做不出来。服务端能不能相信一次通关,取决于它能不能
/// **重新走一遍**玩家声称走过的每一步。同一条平台规则,两种完全不同的机制。
/// </para>
/// <para>
/// 计分按步数。<c>Mistakes</c> 对本游戏结构性地恒为 0(那个计数器只有客户端调
/// <c>check</c> 才增长,而华容道的客户端没有理由调),所以公式**不**看它 ——
/// <c>generalize-puzzle-rules</c> 已经把「计分公式 MUST NOT 要求 Mistakes 被填充过」
/// 写进平台规范,这里是第一个用到它的游戏。
/// </para>
/// </summary>
public sealed class KlotskiRules : IPuzzleRules
{
    /// <summary>与 API 的 camelCase 约定一致;中文不转义,理由同成语纵横。</summary>
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>三星的步数上限 —— 恰好最优。</summary>
    private const double ThreeStarRatio = 1.0;

    /// <summary>二星的步数上限,相对最优。</summary>
    private const double TwoStarRatio = 1.4;

    /// <summary>二星允许的最多提示次数。</summary>
    private const int TwoStarHintBudget = 2;

    /// <inheritdoc />
    public string GameKey => "klotski";

    /// <inheritdoc />
    public PuzzleValidationResult Validate(
        string solutionJson, string layoutJson, string submissionJson)
    {
        var replay = Replay(layoutJson, submissionJson);
        return new PuzzleValidationResult(replay is { Solved: true });
    }

    /// <inheritdoc />
    public PuzzlePartialResult CheckPartial(
        string solutionJson, string layoutJson, string partialJson)
    {
        // 存在是因为接口要求它,并且被调用时必须行为正确 —— 恒返回 false 会污染
        // 服务端的错误计数。**不**是因为预期会被调用:滑动合法性由公开的盘面与公开的
        // 规则决定,客户端自己判得了,为每一步发一个请求既慢又什么都换不来。
        var replay = Replay(layoutJson, partialJson);
        return replay is null
            ? new PuzzlePartialResult(false)
            : new PuzzlePartialResult(
                true,
                JsonSerializer.Serialize(new KlotskiPartialPayload(replay.Solved), Json));
    }

    /// <inheritdoc />
    public PuzzleHintResult Hint(string solutionJson, string layoutJson, string? stateJson)
    {
        var layout = TryDeserialize<KlotskiLayout>(layoutJson);
        if (layout?.Exit is not { } exit || BuildBoard(layout) is not { } initial)
        {
            return new PuzzleHintResult("{}");
        }

        // 从玩家**当前**局面搜,不是从一条预存路径上取下一步:玩家离开那条路径三步
        // 之后,预存的建议既不最优、甚至可能不合法。上报解析不了或描述了一个不合法
        // 局面时退到初始布局 —— 一个没更新的客户端该拿到提示,而不是 400。
        var board = ApplyReportedState(layout, initial, stateJson) ?? initial;

        var solution = KlotskiSolver.Solve(board, exit.Row, exit.Col);
        if (solution is null || solution.Count == 0)
        {
            return new PuzzleHintResult("{}");
        }

        return new PuzzleHintResult(JsonSerializer.Serialize(solution[0], Json));
    }

    /// <inheritdoc />
    public int Score(PuzzleScoreInput input)
    {
        var minMoves = TryDeserialize<KlotskiSolution>(input.SolutionJson)?.MinMoves ?? 0;
        var moves = TryDeserialize<KlotskiSubmission>(input.SubmissionJson)?.Moves?.Count ?? 0;

        // 关卡数据坏了(没有 minMoves)时给最低分而不是抛:计分是通关之后的事,
        // 让玩家的一次通关因为一份坏数据变成 500 是最糟的结果。
        if (minMoves <= 0 || moves <= 0)
        {
            return 1;
        }

        // 提交里的步数**已经被 Validate 重放确认过**,所以它是服务端观测到的事实,
        // 不是客户端的自述 —— 见 PuzzleScoreInput 的文档。这里数的是着法条数本身,
        // 而不是提交里任何客户端自己写的计数字段。
        var ratio = (double)moves / minMoves;

        if (ratio <= ThreeStarRatio && input.HintsUsed == 0)
        {
            return 3;
        }
        if (ratio <= TwoStarRatio && input.HintsUsed <= TwoStarHintBudget)
        {
            return 2;
        }
        return 1;
    }

    /// <summary>一次重放的结果。<c>null</c> 表示途中有一步不合法,或者输入根本解析不了。</summary>
    private sealed record ReplayResult(bool Solved);

    /// <summary>
    /// 从关卡的初始布局出发重放一串移动。任何一步不合法就整份作废 ——
    /// 服务端不接受它重放不出来的东西。
    /// </summary>
    private static ReplayResult? Replay(string layoutJson, string movesJson)
    {
        var layout = TryDeserialize<KlotskiLayout>(layoutJson);
        if (layout?.Exit is not { } exit || BuildBoard(layout) is not { } board)
        {
            return null;
        }

        var submission = TryDeserialize<KlotskiSubmission>(movesJson);
        if (submission?.Moves is null)
        {
            return null;
        }

        foreach (var move in submission.Moves)
        {
            var next = board.TryMove(move);
            if (next is null)
            {
                return null;
            }
            board = next;
        }

        var solved = board.Target is { } target && target.Row == exit.Row && target.Col == exit.Col;
        return new ReplayResult(solved);
    }

    private static KlotskiBoard? BuildBoard(KlotskiLayout layout)
    {
        if (layout.Pieces is null || layout.Pieces.Count == 0)
        {
            return null;
        }

        var pieces = layout.Pieces
            .Select(p => new KlotskiPiece(p.Id, p.Row, p.Col, p.Height, p.Width, p.Target))
            .ToList();

        return KlotskiBoard.TryCreate(layout.Rows, layout.Cols, pieces);
    }

    /// <summary>
    /// 把客户端上报的位置盖到关卡布局上。尺寸与目标标记始终以**关卡**为准 ——
    /// 上报的只是「哪枚子现在在哪」,不是一个可以重新定义棋子的机会。
    /// </summary>
    private static KlotskiBoard? ApplyReportedState(
        KlotskiLayout layout, KlotskiBoard initial, string? stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return null;
        }

        var state = TryDeserialize<KlotskiState>(stateJson);
        if (state?.Pieces is null || state.Pieces.Count == 0)
        {
            return null;
        }

        var byId = state.Pieces.ToDictionary(p => p.Id, p => p, StringComparer.Ordinal);
        var moved = initial.Pieces
            .Select(p => byId.TryGetValue(p.Id, out var reported)
                ? p with { Row = reported.Row, Col = reported.Col }
                : p)
            .ToList();

        // 上报的局面自身不合法(重叠 / 越界)时返回 null,调用方退回初始布局。
        return KlotskiBoard.TryCreate(layout.Rows, layout.Cols, moved);
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        // 载荷来自玩家,畸形输入是正常情况而不是异常情况 —— 一律当作"不正确"处理,
        // 不让一个坏 JSON 变成 500。
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
