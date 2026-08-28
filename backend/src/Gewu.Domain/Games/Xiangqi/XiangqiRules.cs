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

    /// <summary>
    /// 同一个将军最多能重复几次 —— 第 <c>MaxRepeatedChecks + 1</c> 次被拒。
    /// <para>
    /// 一条棋理限制:没有它,一方可以无限长将,对手永远在应将,棋永远走不完,
    /// 而界面上一切正常。
    /// </para>
    /// <para>
    /// **数的是局面,不是走法。** 「将军」是**局面**的性质而不是那一步的性质,所以
    /// 「同一个将军送出了 N 次」与「同一个局面出现了 N 次」是同一句话。因此实现不为
    /// 每一步记「这一步是不是将军」—— 少的那份记录会和局面本身漂开。
    /// </para>
    /// <para>
    /// <b>改这个数要连带改两个 locale 的文案</b> —— `game.errors.repeated-check` 把「三次」
    /// 写在句子里(告诉玩家次数比说一句「太多次了」有用得多),而服务端只发码、不发参数,
    /// 所以那两句只能写死。**没有任何测试会因此变红**:C# 常量与 JSON 文案分别在两套测试的
    /// 视野之外。触发条件写在这里,因为这里是会被改的那一行。
    /// </para>
    /// </summary>
    internal const int MaxRepeatedChecks = 3;

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
    public MoveApplication Apply(MatchState state, MoveIntent intent, int seat)
        => ApplyOn(GameKey, XiangqiBoard.Initial(), state, intent, seat);

    /// <summary>
    /// 走子判定的**唯一一份实现**,起始盘面是参数。
    /// <para>
    /// 它 <c>internal</c> 是为了让 <see cref="XiangqiEndgameRules"/> 从一则残局开局时**共用
    /// 这一份**,而不是持有一份副本 —— 副本会和这里各自漂,而漂的表现是**同一步棋在两个
    /// 房间里一个合法一个不合法**,那种不一致没有任何断言会红,除非有人正好同时在两种
    /// 房间里试同一步。
    /// </para>
    /// <para>
    /// 走子逻辑因此**没有搬家**:既有的一千多条象棋测试仍然打在同一段代码上,
    /// 所以它们是这次「多一个入口没有改行为」的可执行形式。
    /// </para>
    /// </summary>
    /// <param name="gameKey">调用方的棋种键 —— 错误消息要说出**是哪一局**拒绝了这一步,
    /// 而把 <c>xiangqi</c> 写死会让残局房报出来的错说错自己是谁。</param>
    /// <param name="start">这一局从哪块盘面开始。</param>
    /// <param name="state">对局状态。</param>
    /// <param name="intent">这一步。</param>
    /// <param name="seat">走子的座位。</param>
    /// <returns>走完之后的对局状态。</returns>
    internal static MoveApplication ApplyOn(
        string gameKey, XiangqiBoard start, MatchState state, MoveIntent intent, int seat)
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
                $"'{gameKey}' moves pieces; a move must carry an origin square.");
        }

        // 文本载荷在这里被挡下,与连 N 子同理。
        var to = intent.RequirePosition();
        if (!XiangqiBoard.InBounds(from) || !XiangqiBoard.InBounds(to))
        {
            throw new InvalidMoveException(
                $"Position is outside the {XiangqiBoard.RowCount}x{XiangqiBoard.ColCount} "
                + $"board of '{gameKey}'.");
        }

        var board = Replay(start, state.History);

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

        // 长将上限。**顺序要紧:将死在它之前**,所以一步将死的棋永远不会被这条拒掉。
        //
        // 这两条其实撞不上:局面相同 ⇒ 合法着法集合相同 ⇒ 若此刻是将死,此前那次也是将死,
        // 棋在那时就该结束了 —— 一个将死的局面不可能有过往出现。顺序仍然写成这样,
        // 是为了让那段论证不必成为正确性的前提。
        if (IsForbiddenRepeatedCheck(start, state.History, after, side))
        {
            throw InvalidMoveException.RepeatedCheck(
                $"The same check may be given at most {MaxRepeatedChecks} times; "
                + "this move would repeat it once more.");
        }

        return MoveApplication.Ongoing();
    }

    private static Stone Opponent(Stone side) => side == Stone.Black ? Stone.White : Stone.Black;

    /// <summary>
    /// 从走子历史重建局面。历史里的步不再校验 —— 它们当初就是这么被接受的。
    /// <para>
    /// 起始盘面是参数而不是常量:残局从一则古谱的局面开局,而此前这里写死
    /// <c>XiangqiBoard.Initial()</c> —— 那正是「从指定局面开局」这件事唯一挡在路上的一行。
    /// </para>
    /// </summary>
    /// <param name="start">起始盘面。</param>
    /// <param name="history">走子历史。</param>
    /// <param name="onEachPly">
    /// 每一步走完之后回调一次,收到的是**那一刻**的盘面(同一个可变实例,MUST NOT 存下来)。
    /// <para>
    /// 长将上限要数「同一个局面出现过几次」,而那需要每一步之后的中间局面。它是一个回调而不是
    /// 第二个重放循环:两个循环会各自漂,而漂的表现是**计数错**,没有任何断言会红。
    /// 不传时零开销 —— 五子棋量级的路径一步都没变。
    /// </para>
    /// </param>
    private static XiangqiBoard Replay(
        XiangqiBoard start,
        IReadOnlyList<PlayedMove> history,
        Action<XiangqiBoard>? onEachPly = null)
    {
        var board = start.Clone();
        foreach (var played in history)
        {
            if (played.From is { } origin)
            {
                board.Move(origin, played.RequirePosition());
            }
            onEachPly?.Invoke(board);
        }
        return board;
    }

    /// <summary>
    /// 这一步是不是一次**被禁的重复将军** —— <see cref="ApplyOn"/> 与
    /// <see cref="LegalMoves"/> 共用的这一份判断。
    /// <para>
    /// **共用是必须的,不是整洁。** `ai-opponent` 那条要求的 Scenario 写着「`LegalMoves` 的每一条
    /// 都能被 <c>Apply</c> 接受」;两处各写一遍迟早不一致,而不一致的表现是
    /// **AI 走出规则会拒绝的棋**,用户看到的是它卡住了。
    /// </para>
    /// <para>
    /// 局面的身份**就是盘面** —— 象棋里没有王车易位 / 吃过路兵那类额外状态。「下一手轮到谁」
    /// 在这一处不必再算进去,理由在 <see cref="CountEarlierOccurrences"/> 上。
    /// </para>
    /// </summary>
    /// <param name="start">这一局从哪块盘面开始。</param>
    /// <param name="history">这一步之前的全部历史。</param>
    /// <param name="after">这一步走完之后的盘面。</param>
    /// <param name="side">走这一步的一方。</param>
    /// <param name="seat">走这一步的座位。</param>
    private static bool IsForbiddenRepeatedCheck(
        XiangqiBoard start,
        IReadOnlyList<PlayedMove> history,
        XiangqiBoard after,
        Stone side)
    {
        var opponent = Opponent(side);

        // 不将军就不受这条限制 —— 它限制的是**长将**,不是重复本身。双方各自往复一个
        // 不将军的局面,平台不管(而「判和」是另一笔账:见 xiangqi 那条「平台认不出和棋」)。
        if (!IsInCheck(after, opponent))
        {
            return false;
        }

        // 将死优先 —— 而**这个分支没有任何测试能让它红**,所以它需要一段理由而不是一条断言。
        //
        // 「既达到上限又是将死」构造不出来:局面相同 ⇒ 合法着法集合相同(象棋里局面就是
        // 盘面 + 轮到谁)⇒ 若此刻将死,此前那次也将死,棋在那时就该结束了。在任何上限值下都成立。
        //
        // 保留它,是让正确性不依赖上面那段论证。**它变成承重的那一天有名字**:谁把计数的键
        // 从「这一个完整局面」换成更粗的东西(例如为长捉数「这一方将了几次」),不可能就
        // 变成可能,而那时它是唯一挡着的东西。
        //
        // 顺带:它也让本判断在两个调用方里都自洽 —— ApplyOn 已经先判过将死,LegalMoves 没有。
        if (!HasAnyLegalMove(after, opponent))
        {
            return false;
        }

        return CountEarlierOccurrences(start, history, after) >= MaxRepeatedChecks;
    }

    /// <summary>
    /// 历史里出现过几次 <paramref name="target"/> 这个局面。
    /// <para>
    /// 数整局,不是只数最近一个循环 —— 长将可以隔着几手来回。起始局面不算:它不是任何人
    /// 走出来的,所以一个开局就被将着的残局设置不计入「这个将军被送了几次」。
    /// </para>
    /// <para>
    /// <b>它不问「是谁走出这个局面的」,而那不是漏了 —— 是它问不出别的答案。</b> 调用方只在
    /// <paramref name="target"/> 里对手被将时才来数,而**一个「对手被将」的盘面只可能由本方
    /// 走出来**:对手走到那儿等于把自己的将留在被吃的位置,那一步在 <see cref="ApplyOn"/>
    /// 里就被 <c>SelfCheck</c> 挡掉了,进不了历史。所以「本方走出它几次」与「它出现过几次」
    /// 是同一个数。
    /// </para>
    /// <para>
    /// 这里原本有一个 <c>played.Seat == seat</c> 的条件。删掉它的理由不是它便宜 —— 是变异测试
    /// 说它**不可能红**:五条变异里四条被杀,只有「去掉座位判断」活了下来。而上面那段论证
    /// 靠的是自将必被拒,**那条规则自己有测试**。一个靠有测试的规则支撑的删除,好过一个
    /// 没有任何断言能覆盖的分支。
    /// </para>
    /// </summary>
    private static int CountEarlierOccurrences(
        XiangqiBoard start, IReadOnlyList<PlayedMove> history, XiangqiBoard target)
    {
        var count = 0;
        Replay(start, history, board =>
        {
            if (board.SamePosition(target))
            {
                count++;
            }
        });
        return count;
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
    /// 某方在该局面下的**全部合法着法**。
    /// <para>
    /// 判据是「<see cref="Apply"/> 会不会接受它」,而不是一份列举出来的排除项清单 ——
    /// 后者每加一条规则就少一项,而少了的那一项不会有任何断言发现。当前被排除的是:
    /// 自将 / 照面(**局面**的性质),以及超出上限的重复将军(**历史**的性质)。
    /// </para>
    /// <para>
    /// 对外暴露是因为 AI 需要它。让 AI 自己再实现一遍走法枚举就是第二份真源,
    /// 而两份不一致的表现是 **AI 走出规则会拒绝的棋** —— 用户看到的是「机器人卡住了」。
    /// </para>
    /// <para>
    /// <b>返回空表是可能的,而它不再只意味着「棋早该结束了」。</b> 一方的每一条着法都被长将
    /// 上限挡住时,他确实一步也走不了 —— 收场的是回合超时(<c>TurnTimeoutWorker</c> 判走不了
    /// 的一方负),那正是传统的长将判负。
    /// </para>
    /// </summary>
    /// <param name="history">本局已走的全部步,按 Ply 升序。</param>
    /// <param name="side">要枚举哪一方的着法。</param>
    public IReadOnlyList<MoveIntent> LegalMoves(
        IReadOnlyList<PlayedMove> history, Stone side)
    {
        var start = XiangqiBoard.Initial();
        var board = Replay(start, history);

        var permitted = new List<MoveIntent>();
        foreach (var move in LegalMovesOn(board, side))
        {
            var after = board.Clone();
            after.Move(move.From!.Value, move.To!.Value);
            if (!IsForbiddenRepeatedCheck(start, history, after, side))
            {
                permitted.Add(move);
            }
        }
        return permitted;
    }

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
    internal static XiangqiBoard BoardFrom(IReadOnlyList<PlayedMove> history)
        => Replay(XiangqiBoard.Initial(), history);

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

    /// <summary>
    /// 从给定起始盘面重放历史 —— 供 <see cref="XiangqiEndgameRules"/> 用。
    /// <para>与 <see cref="ApplyOn"/> 同一条理由:共用这一份,而不是各持一份副本。</para>
    /// </summary>
    /// <param name="start">起始盘面。</param>
    /// <param name="history">走子历史。</param>
    /// <returns>重放之后的局面。</returns>
    internal static XiangqiBoard BoardFrom(XiangqiBoard start, IReadOnlyList<PlayedMove> history)
        => Replay(start, history);
}
