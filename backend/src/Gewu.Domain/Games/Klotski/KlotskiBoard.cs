using System.Text;

namespace Gewu.Domain.Games.Klotski;

/// <summary>
/// 华容道盘面。**规则与求解器内部使用** —— 谜题内核看不到它,只看见不透明的 JSON。
/// <para>
/// 盘面尺寸来自关卡而不是常数:经典局面是 5×4,但没有理由把它写死。
/// </para>
/// <para>
/// 非线程安全。每次 <c>Validate</c> / <c>Hint</c> 从布局新建一块,不跨调用共享。
/// </para>
/// </summary>
internal sealed class KlotskiBoard
{
    private readonly KlotskiPiece[] _pieces;
    private readonly int[] _occupancy;

    private KlotskiBoard(int rows, int cols, KlotskiPiece[] pieces, int[] occupancy)
    {
        Rows = rows;
        Cols = cols;
        _pieces = pieces;
        _occupancy = occupancy;
    }

    /// <summary>行数。</summary>
    internal int Rows { get; }

    /// <summary>列数。</summary>
    internal int Cols { get; }

    /// <summary>盘上的棋子,顺序与建盘时一致。</summary>
    internal IReadOnlyList<KlotskiPiece> Pieces => _pieces;

    /// <summary>
    /// 摆一块盘。棋子越界或互相重叠时返回 <c>null</c> —— 一份坏关卡是数据问题,
    /// 调用方把它当作「不通关」处理,而不是让它变成 500。
    /// </summary>
    /// <param name="rows">行数。</param>
    /// <param name="cols">列数。</param>
    /// <param name="pieces">棋子。</param>
    internal static KlotskiBoard? TryCreate(int rows, int cols, IReadOnlyList<KlotskiPiece> pieces)
    {
        if (rows <= 0 || cols <= 0 || pieces is null)
        {
            return null;
        }

        var occupancy = new int[rows * cols];
        Array.Fill(occupancy, -1);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < pieces.Count; i++)
        {
            var piece = pieces[i];
            if (piece.Height <= 0 || piece.Width <= 0 || !ids.Add(piece.Id))
            {
                return null;
            }
            if (piece.Row < 0 || piece.Col < 0 ||
                piece.Row + piece.Height > rows || piece.Col + piece.Width > cols)
            {
                return null;
            }

            for (var r = piece.Row; r < piece.Row + piece.Height; r++)
            {
                for (var c = piece.Col; c < piece.Col + piece.Width; c++)
                {
                    if (occupancy[(r * cols) + c] != -1)
                    {
                        return null;
                    }
                    occupancy[(r * cols) + c] = i;
                }
            }
        }

        return new KlotskiBoard(rows, cols, [.. pieces], occupancy);
    }

    /// <summary>取指定 id 的棋子下标;不存在返回 <c>-1</c>。</summary>
    /// <param name="id">棋子标识。</param>
    internal int IndexOf(string id)
    {
        for (var i = 0; i < _pieces.Length; i++)
        {
            if (string.Equals(_pieces[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>要送到出口的那一枚;关卡没有标记则返回 <c>null</c>。</summary>
    internal KlotskiPiece? Target
    {
        get
        {
            foreach (var piece in _pieces)
            {
                if (piece.IsTarget)
                {
                    return piece;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// 走一步。合法则返回**新**盘面,不合法返回 <c>null</c> —— 盘面不可变,
    /// 搜索因此可以放心地把局面塞进集合里。
    /// </summary>
    /// <param name="move">要走的一步。</param>
    internal KlotskiBoard? TryMove(KlotskiMove move)
    {
        if (!move.IsSingleStep)
        {
            return null;
        }

        var index = IndexOf(move.Id);
        if (index < 0)
        {
            return null;
        }

        return TryMoveAt(index, move.Dr, move.Dc);
    }

    /// <summary>按下标走一步 —— 搜索内部用,省掉 id 查找。</summary>
    internal KlotskiBoard? TryMoveAt(int index, int dr, int dc)
    {
        var piece = _pieces[index];
        var moved = piece.Shifted(dr, dc);

        if (moved.Row < 0 || moved.Col < 0 ||
            moved.Row + moved.Height > Rows || moved.Col + moved.Width > Cols)
        {
            return null;
        }

        // 目标格要么本来就空,要么原本就属于这枚子自己(沿移动方向重叠的那部分)。
        for (var r = moved.Row; r < moved.Row + moved.Height; r++)
        {
            for (var c = moved.Col; c < moved.Col + moved.Width; c++)
            {
                var occupant = _occupancy[(r * Cols) + c];
                if (occupant != -1 && occupant != index)
                {
                    return null;
                }
            }
        }

        var pieces = (KlotskiPiece[])_pieces.Clone();
        pieces[index] = moved;

        var occupancy = new int[Rows * Cols];
        Array.Fill(occupancy, -1);
        for (var i = 0; i < pieces.Length; i++)
        {
            var p = pieces[i];
            for (var r = p.Row; r < p.Row + p.Height; r++)
            {
                for (var c = p.Col; c < p.Col + p.Width; c++)
                {
                    occupancy[(r * Cols) + c] = i;
                }
            }
        }

        return new KlotskiBoard(Rows, Cols, pieces, occupancy);
    }

    /// <summary>
    /// 搜索用的局面签名:每格记**形状**,而不是棋子 id。
    /// <para>
    /// 两枚 1×1 卒交换位置得到的是同一个局面。不做这个归一化,状态空间会因为
    /// 4 枚卒与 4 枚竖将的排列而膨胀两个数量级,而且求出来的「最优」会把
    /// 「把两枚卒换个位置」也算成有意义的步数 —— 那不是玩家眼里的一步。
    /// </para>
    /// <para>
    /// 目标子(曹操)记成一个**独有**的符号,不与同形状的子归并 —— 否则一个含两枚
    /// 2×2 的自造关卡会把「送错一枚出去」当成通关。经典局面只有一枚 2×2,但一条
    /// 只在经典局面下成立的归一化不值得留。
    /// </para>
    /// </summary>
    internal string Signature()
    {
        var cells = new char[Rows * Cols];
        for (var i = 0; i < cells.Length; i++)
        {
            var occupant = _occupancy[i];
            if (occupant == -1)
            {
                cells[i] = '.';
                continue;
            }
            var piece = _pieces[occupant];
            cells[i] = piece.IsTarget
                ? '@'
                : (char)('a' + Math.Min(25, ((piece.Height - 1) * 5) + (piece.Width - 1)));
        }
        return new string(cells);
    }

    /// <summary>调试友好的多行盘面。</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        var signature = Signature();
        for (var r = 0; r < Rows; r++)
        {
            sb.AppendLine(signature.Substring(r * Cols, Cols));
        }
        return sb.ToString();
    }
}
