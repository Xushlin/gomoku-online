using System.Text.Json.Serialization;

namespace Gewu.Domain.Games.Klotski;

/// <summary>关卡布局里的一枚子。人物名之类的显示字段在这里被忽略 —— 领域层不需要它们。</summary>
/// <param name="Id">棋子标识。</param>
/// <param name="Row">左上角行。</param>
/// <param name="Col">左上角列。</param>
/// <param name="Height">占几行。</param>
/// <param name="Width">占几列。</param>
/// <param name="Target">是否是要送到出口的那一枚。</param>
public sealed record KlotskiLayoutPiece(
    string Id,
    int Row,
    int Col,
    int Height,
    int Width,
    [property: JsonPropertyName("target")] bool Target = false);

/// <summary>出口 —— 目标子的左上角必须到达的格。</summary>
/// <param name="Row">行。</param>
/// <param name="Col">列。</param>
public sealed record KlotskiExit(int Row, int Col);

/// <summary>
/// 关卡布局。**会**下发客户端 —— 华容道没有秘密,客户端必须知道这些才能画盘、才能滑动。
/// </summary>
/// <param name="Rows">行数。</param>
/// <param name="Cols">列数。</param>
/// <param name="Exit">出口。</param>
/// <param name="Pieces">棋子。</param>
public sealed record KlotskiLayout(
    int Rows,
    int Cols,
    KlotskiExit? Exit,
    IReadOnlyList<KlotskiLayoutPiece>? Pieces);

/// <summary>
/// 关卡「答案」。
/// <para>
/// 只有一个计分参数,因为**华容道没有要藏的东西**:棋子、盘面、出口、滑动规则全部
/// 公开且全部在客户端(一个判不了滑动的客户端连动画都做不出来)。它的服务端权威
/// 来自**重放**而不是**隐藏** —— 见 <see cref="KlotskiRules.Validate"/>。
/// </para>
/// <para>
/// 给它编造一份「标准解」只会让下一个读代码的人以为那里有秘密。
/// </para>
/// </summary>
/// <param name="MinMoves">最优步数,由 <see cref="KlotskiSolver"/> 离线算出。计分拿它当分母。</param>
public sealed record KlotskiSolution(int MinMoves);

/// <summary>玩家的提交:一串一格移动。</summary>
/// <param name="Moves">移动序列。</param>
public sealed record KlotskiSubmission(IReadOnlyList<KlotskiMove>? Moves);

/// <summary>
/// 客户端上报的当前局面 —— 只有每枚子的位置,尺寸与目标标记仍以关卡布局为准。
/// </summary>
/// <param name="Pieces">当前位置。</param>
public sealed record KlotskiState(IReadOnlyList<KlotskiStatePiece>? Pieces);

/// <summary>上报局面里的一枚子。</summary>
/// <param name="Id">棋子标识。</param>
/// <param name="Row">当前左上角行。</param>
/// <param name="Col">当前左上角列。</param>
public sealed record KlotskiStatePiece(string Id, int Row, int Col);

/// <summary><c>CheckPartial</c> 判定为真时附带的载荷。</summary>
/// <param name="CaoCaoOut">这段前缀走完之后,目标子是否已经在出口。</param>
public sealed record KlotskiPartialPayload(bool CaoCaoOut);
