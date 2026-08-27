using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.Xiangqi;

/// <summary>
/// 中国象棋盘面,10 行 × 9 列。**规则内部使用** —— 聚合根看不到它,这正是
/// <c>generalize-match-domain</c> 把盘面语义下沉进 <c>IGameRules</c> 的目的。
/// <para>
/// 方位约定(见 <see cref="XiangqiRules"/>):<see cref="Stone.Black"/> 是**红方**,占第 5–9 行
/// (第 9 行是它的底线);<see cref="Stone.White"/> 是黑方,占第 0–4 行。楚河汉界在第 4 与第 5 行之间。
/// </para>
/// <para>
/// 非线程安全 —— 每次 <c>Apply</c> 从历史新建一块,不跨调用共享。
/// </para>
/// </summary>
internal sealed class XiangqiBoard
{
    /// <summary>行数。</summary>
    internal const int RowCount = 10;

    /// <summary>列数。</summary>
    internal const int ColCount = 9;

    private readonly XiangqiPiece?[] _cells = new XiangqiPiece?[RowCount * ColCount];

    private XiangqiBoard()
    {
    }

    /// <summary>摆好开局的棋盘。</summary>
    /// <summary>一块空盘 —— 残局从设置摆子时的起点。</summary>
    /// <returns>没有任何棋子的局面。</returns>
    internal static XiangqiBoard Empty() => new();

    internal static XiangqiBoard Initial()
    {
        var board = new XiangqiBoard();

        // 黑方(Stone.White)在上,第 0 行是它的底线。
        board.PlaceBackRank(0, Stone.White);
        board.Set(2, 1, new XiangqiPiece(XiangqiPieceType.Cannon, Stone.White));
        board.Set(2, 7, new XiangqiPiece(XiangqiPieceType.Cannon, Stone.White));
        for (var col = 0; col < ColCount; col += 2)
        {
            board.Set(3, col, new XiangqiPiece(XiangqiPieceType.Soldier, Stone.White));
        }

        // 红方(Stone.Black)在下,第 9 行是它的底线。红先手 —— 与 Game.CurrentTurn 的初值对齐。
        board.PlaceBackRank(9, Stone.Black);
        board.Set(7, 1, new XiangqiPiece(XiangqiPieceType.Cannon, Stone.Black));
        board.Set(7, 7, new XiangqiPiece(XiangqiPieceType.Cannon, Stone.Black));
        for (var col = 0; col < ColCount; col += 2)
        {
            board.Set(6, col, new XiangqiPiece(XiangqiPieceType.Soldier, Stone.Black));
        }

        return board;
    }

    private void PlaceBackRank(int row, Stone side)
    {
        XiangqiPieceType[] order =
        [
            XiangqiPieceType.Chariot, XiangqiPieceType.Horse, XiangqiPieceType.Elephant,
            XiangqiPieceType.Advisor, XiangqiPieceType.General, XiangqiPieceType.Advisor,
            XiangqiPieceType.Elephant, XiangqiPieceType.Horse, XiangqiPieceType.Chariot,
        ];
        for (var col = 0; col < ColCount; col++)
        {
            Set(row, col, new XiangqiPiece(order[col], side));
        }
    }

    /// <summary>该坐标是否在盘内。</summary>
    internal static bool InBounds(Position p) => p.Row < RowCount && p.Col < ColCount;

    /// <summary>取某格的棋子;空格返回 <c>null</c>。</summary>
    internal XiangqiPiece? At(Position p) => _cells[(p.Row * ColCount) + p.Col];

    /// <summary>取某格的棋子;空格返回 <c>null</c>。</summary>
    internal XiangqiPiece? At(int row, int col) => _cells[(row * ColCount) + col];

    internal void Set(int row, int col, XiangqiPiece? piece) => _cells[(row * ColCount) + col] = piece;

    /// <summary>
    /// 走一步 —— **不做任何校验**,合法性由 <see cref="XiangqiRules"/> 判定。
    /// 起点清空,终点覆盖(被吃的子就此消失)。
    /// </summary>
    internal void Move(Position from, Position to)
    {
        var piece = At(from);
        Set(from.Row, from.Col, null);
        Set(to.Row, to.Col, piece);
    }

    /// <summary>复制一份,供「试走一步看会不会自将」使用。</summary>
    internal XiangqiBoard Clone()
    {
        var clone = new XiangqiBoard();
        Array.Copy(_cells, clone._cells, _cells.Length);
        return clone;
    }

    /// <summary>找某方的将帅;被吃掉(理论上不该发生)时返回 <c>null</c>。</summary>
    internal Position? FindGeneral(Stone side)
    {
        for (var row = 0; row < RowCount; row++)
        {
            for (var col = 0; col < ColCount; col++)
            {
                if (At(row, col) is { Type: XiangqiPieceType.General } g && g.Side == side)
                {
                    return new Position(row, col);
                }
            }
        }
        return null;
    }

    /// <summary>枚举某方所有棋子的位置。</summary>
    internal IEnumerable<(Position Position, XiangqiPiece Piece)> PiecesOf(Stone side)
    {
        for (var row = 0; row < RowCount; row++)
        {
            for (var col = 0; col < ColCount; col++)
            {
                if (At(row, col) is { } piece && piece.Side == side)
                {
                    yield return (new Position(row, col), piece);
                }
            }
        }
    }

    /// <summary>两格之间(同行或同列,不含两端)夹着几个子。不同行不同列时返回 <c>-1</c>。</summary>
    internal int CountBetween(Position a, Position b)
    {
        if (a.Row != b.Row && a.Col != b.Col)
        {
            return -1;
        }

        var stepRow = Math.Sign(b.Row - a.Row);
        var stepCol = Math.Sign(b.Col - a.Col);
        var count = 0;

        var row = a.Row + stepRow;
        var col = a.Col + stepCol;
        while (row != b.Row || col != b.Col)
        {
            if (At(row, col) is not null)
            {
                count++;
            }
            row += stepRow;
            col += stepCol;
        }
        return count;
    }
}
