using Gewu.Domain.Manuals;

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
/// <param name="Verdict">谱主的评断 —— 见 <see cref="Gewu.Domain.Manuals.ManualVerdict"/>。</param>
/// <param name="PieceCount">起始局面上的子数;界面据此区分残局与满盘,**而它不是「是不是标准开局」的判据**。</param>
public sealed record ManualLineSummaryDto(
    int Id, string Title, int MoveCount, ManualVerdict Verdict, int PieceCount);

/// <summary>目录里的一局及其变化。</summary>
/// <param name="Chapter">局号,1 起。</param>
/// <param name="Lines">该局下的变化,按局内次序。</param>
public sealed record ManualChapterDto(int Chapter, IReadOnlyList<ManualLineSummaryDto> Lines);

/// <summary>一部古谱的目录。</summary>
/// <param name="ManualKey">古谱键。</param>
/// <param name="Name">书名(原书名,公有领域)—— 目录页的标题用它,而不是一份客户端的键到名字的映射。</param>
/// <param name="Grouped">这部谱有没有「第N局」那一层。</param>
/// <param name="GameKey">棋种键 —— 前端据此挑只读棋盘,与回放页同一条理由。</param>
/// <param name="Chapters">
/// 按局号升序。**可以只有一层** —— 六辑残局没有「第N局」这一层,而为了形状一致给它们
/// 编一个局号是编数据。那种情况下这里是**一个** chapter,其 <c>Chapter</c> 为 0。
/// </param>
public sealed record ManualCatalogueDto(
    string ManualKey,
    string Name,
    bool Grouped,
    string GameKey,
    IReadOnlyList<ManualChapterDto> Chapters);

/// <summary>古谱清单里的一部。</summary>
/// <param name="ManualKey">古谱键。</param>
/// <param name="Name">书名(原书名,公有领域)。</param>
/// <param name="LineCount">条数。</param>
/// <param name="Grouped">这部谱有没有「第N局」那一层。</param>
public sealed record ManualSummaryDto(string ManualKey, string Name, int LineCount, bool Grouped);

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
/// <param name="Verdict">
/// 谱主的评断 —— **是评断,不是终局**。《梅花谱》31 条里只有 11 条真的走到将死,
/// 其余走到「优势已成」就停,所以前端 MUST NOT 把它说成「将死」。
/// </param>
/// <param name="StartPosition">
/// 起始局面,90 字符的行优先盘面串(`.` 空格,红大写黑小写)。**首帧就是这个局面** ——
/// 让棋盘从标准开局重放,会把一条 10 子的残局画成 32 子加几步棋:一个看起来完全正常的错盘面。
/// </param>
/// <param name="FirstSeat">先走方座位(0 = 红)。1634 局里 7 局是黑先走,所以它是数据不是约定。</param>
/// <param name="Moves">着法,按 <c>Ply</c> 升序。</param>
public sealed record ManualLineDto(
    int Id,
    string ManualKey,
    string GameKey,
    int Chapter,
    string Title,
    ManualVerdict Verdict,
    string StartPosition,
    int FirstSeat,
    IReadOnlyList<ManualMoveDto> Moves);
