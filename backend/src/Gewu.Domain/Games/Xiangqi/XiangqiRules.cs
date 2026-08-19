using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.Xiangqi;

/// <summary>
/// 中国象棋规则。
/// <para>
/// **本棋种中 <see cref="Stone.Black"/> 是红方,<see cref="Stone.White"/> 是黑方。**
/// 理由是先手:<c>Game</c> 初始化 <c>CurrentTurn = Stone.Black</c>,而象棋红先。
/// <c>Stone</c> 在 Domain 里的含义本就是「先手方 / 后手方」,红黑是**显示层**怎么画它 ——
/// 与 <c>BlackPlayerId</c> / <c>WhitePlayerId</c> 就是两个座位是同一件事。
/// 读代码时这里容易绊一下,所以写在这儿并且有测试钉着。
/// </para>
/// <para>
/// 盘面表示(<see cref="XiangqiBoard"/>)完全内部:聚合根只交出走子历史,盘面怎么重建是规则的私事。
/// 本类**不实现 <see cref="INInARowRules"/>** —— 象棋没有「连几子」,也不用 <c>Board</c>。
/// </para>
/// <para>
/// 无状态,可安全地被并发的多个房间共享。
/// </para>
/// </summary>
public sealed class XiangqiRules : IBoardGameRules
{
    /// <summary>红方(<see cref="Stone.Black"/>)一侧的底线行号。</summary>
    private const int RedHomeRow = 9;

    /// <inheritdoc />
    public string GameKey => GameKeys.Xiangqi;

    /// <inheritdoc />
    public int Rows => XiangqiBoard.RowCount;

    /// <inheritdoc />
    public int Cols => XiangqiBoard.ColCount;

    /// <summary>
    /// 开放人人对战。
    /// <para>
    /// 这是**推论**,不是判断。<c>enforce-human-vs-human</c> 给这个字段定的含义是「平台是否提供
    /// 人人对战入口」,而判据是行为不是意图:只要 <c>POST /api/rooms</c> 接受这个棋种,入口就
    /// **确实**存在。大厅泛化之后 <c>/g/xiangqi/lobby</c> 是一个真实可用的页面,象棋走的是同一个
    /// <c>Room</c> 聚合、同一套建房与加入,所以声明只能跟上。
    /// </para>
    /// <para>
    /// 本字段此前是 <c>false</c>,注释写着「大厅泛化之后翻它」—— 那是一个**自己写下触发条件的
    /// 推迟**,而触发条件已经到了。
    /// </para>
    /// </summary>
    /// <inheritdoc />
    public int SeatCount => BoardSeats.SeatCount;

    public bool SupportsHumanVsHuman => true;

    /// <summary>
    /// 计分。
    /// <para>
    /// 与上一个字段不同,这一条是**判断**,所以它需要一个写下来的理由 —— 而这正是本类上一版
    /// 预告过的那个决定(「计不计分是那时一个独立的、需要理由的决定」)。
    /// </para>
    /// <para>
    /// 理由:象棋此前不计分的**唯一**依据是「没有对手池,阶梯量不出棋力」,而开放人人对战正好
    /// 消灭了那条依据。剩下的形状与五子棋逐项相同 —— 有真实的人类对手池,也有 AI,而机器人对局
    /// 计分是 <c>ai-opponent</c> D7 的反套利规则,不是漏洞。
    /// </para>
    /// <para>
    /// 不变量 <c>IsRated ⇒ SupportsHumanVsHuman</c> 仍然成立(true ⇒ true),并且仍然由遍历
    /// 注册表的测试强制 —— 本类不走 <c>NInARowRules</c> 的构造器,所以那条遍历是它唯一的机制。
    /// </para>
    /// </summary>
    public bool IsRated => true;

    /// <inheritdoc />
    public MoveApplication Apply(
        IReadOnlyList<PlayedMove> history, MoveIntent intent, int seat)
    {
        // 座位 0 = 先手 = 红。「Stone.Black 就是红」那条读法在这里落成结构。
        var side = BoardSeats.ToStone(seat);

        if (side == Stone.Empty)
        {
            throw new InvalidMoveException("Move side cannot be Stone.Empty; use Black or White.");
        }

        // 形状校验属于规则。象棋是**走子类**:一步棋必须说清从哪儿到哪儿。
        if (intent.From is not { } from)
        {
            throw new InvalidMoveException(
                $"'{GameKey}' moves pieces; a move must carry an origin square.");
        }

        // 文本载荷在这里被挡下,与连 N 子同理。
        var to = intent.RequirePosition();
        if (!XiangqiBoard.InBounds(from) || !XiangqiBoard.InBounds(to))
        {
            throw new InvalidMoveException(
                $"Position is outside the {Rows}x{Cols} board of '{GameKey}'.");
        }

        var board = Replay(history);

        var piece = board.At(from)
            ?? throw new InvalidMoveException(
                $"There is no piece at ({from.Row}, {from.Col}).");

        if (piece.Side != side)
        {
            throw new InvalidMoveException(
                $"The piece at ({from.Row}, {from.Col}) does not belong to {side}.");
        }

        if (from == to)
        {
            throw new InvalidMoveException("A move must change the piece's square.");
        }

        if (board.At(to) is { } target && target.Side == side)
        {
            throw new InvalidMoveException(
                $"({to.Row}, {to.Col}) is occupied by your own piece.");
        }

        if (!IsPseudoLegal(board, piece, from, to))
        {
            throw new InvalidMoveException(
                $"A {piece.Type} cannot move from ({from.Row}, {from.Col}) to ({to.Row}, {to.Col}).");
        }

        // 送将 / 自将 / 将帅照面 —— 三者在实现上是同一条:走完之后本方将帅不得被攻击。
        // 照面之所以不需要单写一条特判,是因为它等价于「敌将沿该列可以直吃」,见 IsAttacked。
        var after = board.Clone();
        after.Move(from, to);
        if (IsInCheck(after, side))
        {
            throw InvalidMoveException.SelfCheck(
                "That move would leave your general in check (self-check or flying generals).");
        }

        // 对方没有任何合法走法就输了 —— 将死与困毙**都判负**,这一点与国际象棋不同
        // (那里困毙是和棋)。
        var opponent = Opponent(side);
        if (!HasAnyLegalMove(after, opponent))
        {
            // 赢家是走子方,而"走子方"在这一层就是 seat —— 此前这里写的是
            // `side == Stone.Black ? BlackWin : WhiteWin`,即把三行之前刚从 seat 换出来的
            // 那个颜色又换了回去。
            return MoveApplication.Won(seat);
        }

        return MoveApplication.Ongoing();
    }

    private static Stone Opponent(Stone side) => side == Stone.Black ? Stone.White : Stone.Black;

    /// <summary>从走子历史重建局面。历史里的步不再校验 —— 它们当初就是这么被接受的。</summary>
    private static XiangqiBoard Replay(IReadOnlyList<PlayedMove> history)
    {
        var board = XiangqiBoard.Initial();
        foreach (var played in history)
        {
            if (played.From is { } origin)
            {
                board.Move(origin, played.RequirePosition());
            }
        }
        return board;
    }

    /// <summary>本方是红方吗 —— 红方在下(第 5–9 行),兵朝行号减小的方向走。</summary>
    private static bool IsRed(Stone side) => side == Stone.Black;

    /// <summary>该格是否在某方的九宫内。</summary>
    private static bool InPalace(Stone side, Position p)
    {
        if (p.Col is < 3 or > 5)
        {
            return false;
        }
        return IsRed(side) ? p.Row >= 7 : p.Row <= 2;
    }

    /// <summary>该格是否还在某方自己的河界这一侧(象不得过河)。</summary>
    private static bool OnOwnSide(Stone side, Position p) => IsRed(side) ? p.Row >= 5 : p.Row <= 4;

    /// <summary>
    /// 只看走法本身,不管走完会不会自将。
    /// </summary>
    private static bool IsPseudoLegal(
        XiangqiBoard board, XiangqiPiece piece, Position from, Position to)
    {
        var dRow = to.Row - from.Row;
        var dCol = to.Col - from.Col;
        var absRow = Math.Abs(dRow);
        var absCol = Math.Abs(dCol);

        switch (piece.Type)
        {
            case XiangqiPieceType.General:
                return absRow + absCol == 1 && InPalace(piece.Side, to);

            case XiangqiPieceType.Advisor:
                return absRow == 1 && absCol == 1 && InPalace(piece.Side, to);

            case XiangqiPieceType.Elephant:
                // 田字 + 不过河 + 塞象眼(田字中心有子则不可走)。
                if (absRow != 2 || absCol != 2 || !OnOwnSide(piece.Side, to))
                {
                    return false;
                }
                return board.At(from.Row + (dRow / 2), from.Col + (dCol / 2)) is null;

            case XiangqiPieceType.Horse:
                // 日字 + 蹩马腿:挡住的是**长边方向**的那一格,不是斜对角。
                if (!((absRow == 2 && absCol == 1) || (absRow == 1 && absCol == 2)))
                {
                    return false;
                }
                var legRow = from.Row + (absRow == 2 ? Math.Sign(dRow) : 0);
                var legCol = from.Col + (absCol == 2 ? Math.Sign(dCol) : 0);
                return board.At(legRow, legCol) is null;

            case XiangqiPieceType.Chariot:
                return (dRow == 0 || dCol == 0) && board.CountBetween(from, to) == 0;

            case XiangqiPieceType.Cannon:
                if (dRow != 0 && dCol != 0)
                {
                    return false;
                }
                var between = board.CountBetween(from, to);
                // 吃子时中间必须恰有一个子(炮架);不吃子时中间不得有子。
                return board.At(to) is null ? between == 0 : between == 1;

            case XiangqiPieceType.Soldier:
                var forward = IsRed(piece.Side) ? -1 : 1;
                if (dRow == forward && dCol == 0)
                {
                    return true;
                }
                // 过河之后才能横走一步;永不后退。
                var crossed = !OnOwnSide(piece.Side, from);
                return crossed && dRow == 0 && absCol == 1;

            default:
                return false;
        }
    }

    /// <summary>
    /// 某方的将帅此刻是否被攻击 —— 「被将军」与「将帅照面」在这里是同一件事。
    /// <para>
    /// 照面不需要单独的特判:两将同列且中间无子时,敌方将帅按 <see cref="IsPseudoLegal"/> 的
    /// 车式判定本来就吃不到(将只能走一步),所以这里对 <see cref="XiangqiPieceType.General"/>
    /// 额外按「沿该列直吃」处理 —— 那正是照面规则的内容。
    /// </para>
    /// </summary>
    private static bool IsInCheck(XiangqiBoard board, Stone side)
    {
        if (board.FindGeneral(side) is not { } general)
        {
            // 将帅不在盘上:上一步把它吃了。当作被将军 —— 这一步不该被允许。
            return true;
        }

        foreach (var (position, piece) in board.PiecesOf(Opponent(side)))
        {
            if (piece.Type == XiangqiPieceType.General)
            {
                // 将帅照面:同列、中间无子即可「直吃」。
                if (position.Col == general.Col && board.CountBetween(position, general) == 0)
                {
                    return true;
                }
                continue;
            }

            if (IsPseudoLegal(board, piece, position, general))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 某方在该局面下的**全部合法着法**(已排除会导致自将 / 照面的)。
    /// <para>
    /// 对外暴露是因为 AI 需要它。让 AI 自己再实现一遍走法枚举就是第二份真源,
    /// 而两份不一致的表现是 **AI 走出规则会拒绝的棋** —— 用户看到的是「机器人卡住了」。
    /// </para>
    /// </summary>
    /// <param name="history">本局已走的全部步,按 Ply 升序。</param>
    /// <param name="side">要枚举哪一方的着法。</param>
    public IReadOnlyList<MoveIntent> LegalMoves(
        IReadOnlyList<PlayedMove> history, Stone side)
        => LegalMovesOn(Replay(history), side);

    private static List<MoveIntent> LegalMovesOn(XiangqiBoard board, Stone side)
    {
        var moves = new List<MoveIntent>();
        foreach (var (from, piece) in board.PiecesOf(side).ToList())
        {
            for (var row = 0; row < XiangqiBoard.RowCount; row++)
            {
                for (var col = 0; col < XiangqiBoard.ColCount; col++)
                {
                    var to = new Position(row, col);
                    if (from == to)
                    {
                        continue;
                    }
                    if (board.At(to) is { } occupant && occupant.Side == side)
                    {
                        continue;
                    }
                    if (!IsPseudoLegal(board, piece, from, to))
                    {
                        continue;
                    }

                    var after = board.Clone();
                    after.Move(from, to);
                    if (!IsInCheck(after, side))
                    {
                        moves.Add(MoveIntent.Slide(from, to));
                    }
                }
            }
        }
        return moves;
    }

    /// <summary>
    /// 某方还有没有任何一步合法走法。没有 = 将死或困毙,两者都判负。
    /// <para>
    /// 走 <see cref="LegalMovesOn"/> 而不是自己再写一遍循环 —— 两份枚举迟早不一致,
    /// 而不一致的表现是「判负了但其实有棋走」。多枚举几步的开销在这里无关紧要。
    /// </para>
    /// </summary>
    private static bool HasAnyLegalMove(XiangqiBoard board, Stone side)
        => LegalMovesOn(board, side).Count > 0;

    /// <summary>供 AI 查看局面的只读入口 —— 返回一份副本,调用方改不到规则的东西。</summary>
    /// <param name="history">本局已走的全部步,按 Ply 升序。</param>
    internal static XiangqiBoard BoardFrom(IReadOnlyList<PlayedMove> history) => Replay(history);

    /// <summary>
    /// 直接在一块盘面上枚举合法着法 —— 供 AI 搜索使用。
    /// <para>
    /// 与 <see cref="LegalMoves"/> 同一份实现,只是免去重放:搜索每往下一层都重放一遍历史,
    /// 会把 O(b^d) 变成 O(b^d · n)。**判定逻辑仍然只有一份**,这才是重点 ——
    /// AI 与规则对「什么是合法着法」的看法不可能分叉。
    /// </para>
    /// </summary>
    /// <param name="board">局面。</param>
    /// <param name="side">要枚举哪一方的着法。</param>
    internal static List<MoveIntent> LegalMovesOnBoard(XiangqiBoard board, Stone side)
        => LegalMovesOn(board, side);
}
