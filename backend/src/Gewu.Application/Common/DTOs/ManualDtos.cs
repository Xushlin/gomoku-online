namespace Gewu.Application.Common.DTOs;

/// <summary>
/// 古谱目录里的一条线路。
/// <para>
/// <paramref name="MoveCount"/> 是**算出来的**(着法数组的长度),不是存的一列 ——
/// 一个与着法并存的计数是第二份真源,漂移的那天没有东西会报。
/// </para>
/// </summary>
/// <param name="Id">线路主键,学习页用它取详情。</param>
/// <param name="Title">原书局名。</param>
/// <param name="MoveCount">半手数。</param>
/// <param name="WinnerSeat">谱主判占优的座位(0 = 红先手)。</param>
public sealed record ManualLineSummaryDto(int Id, string Title, int MoveCount, int WinnerSeat);

/// <summary>目录里的一局及其变化。</summary>
/// <param name="Chapter">局号,1 起。</param>
/// <param name="Lines">该局下的变化,按局内次序。</param>
public sealed record ManualChapterDto(int Chapter, IReadOnlyList<ManualLineSummaryDto> Lines);

/// <summary>一部古谱的目录。</summary>
/// <param name="ManualKey">古谱键。</param>
/// <param name="GameKey">棋种键 —— 前端据此挑只读棋盘,与回放页同一条理由。</param>
/// <param name="Chapters">按局号升序。</param>
public sealed record ManualCatalogueDto(
    string ManualKey,
    string GameKey,
    IReadOnlyList<ManualChapterDto> Chapters);

/// <summary>
/// 古谱里的一手。
/// <para>
/// 它**不是** <see cref="MoveDto"/>,而这是量过的取舍:象棋棋盘只读起点与终点四个数,
/// 不读 <c>playedAt</c>。给古谱的每一手编一个时间戳只为了「形状一致」,会得到一份
/// **看起来和真的一模一样**的假数据。所以这里窄一点,前端在页面边界上映射一次 ——
/// 回放页本来就在合成 <c>RoomState</c>,那是同一类映射。
/// </para>
/// </summary>
/// <param name="Ply">第几手,1 起。</param>
/// <param name="FromRow">起点行。</param>
/// <param name="FromCol">起点列。</param>
/// <param name="Row">终点行。</param>
/// <param name="Col">终点列。</param>
/// <param name="Seat">走这一手的座位(0 = 红先手)。</param>
public sealed record ManualMoveDto(int Ply, int FromRow, int FromCol, int Row, int Col, int Seat);

/// <summary>一条古谱线路的完整内容。</summary>
/// <param name="Id">线路主键。</param>
/// <param name="ManualKey">古谱键。</param>
/// <param name="GameKey">棋种键。</param>
/// <param name="Chapter">局号。</param>
/// <param name="Title">原书局名。</param>
/// <param name="WinnerSeat">
/// 谱主判占优的座位 —— **是评断,不是终局**。31 条线路里只有 11 条真的走到将死,
/// 其余走到「优势已成」就停,所以前端 MUST NOT 把它说成「将死」。
/// </param>
/// <param name="Moves">着法,按 <c>Ply</c> 升序。</param>
public sealed record ManualLineDto(
    int Id,
    string ManualKey,
    string GameKey,
    int Chapter,
    string Title,
    int WinnerSeat,
    IReadOnlyList<ManualMoveDto> Moves);
