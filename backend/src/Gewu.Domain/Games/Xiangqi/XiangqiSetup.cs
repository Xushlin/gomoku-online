using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.Xiangqi;

/// <summary>
/// 一局残局的设置:**从哪个局面开始**,以及**谁先走**。
/// <para>
/// 编码是 <c>&lt;90 字符盘面串&gt;:&lt;先走方座位&gt;</c>。盘面串与古谱线路存的是**同一种表示**
/// (行优先、`.` 空格、红大写黑小写),而 MUST NOT 为了这里再造第二种 —— 两种表示会各自漂,
/// 而漂的表现是「研习页画的局面和对弈开出来的不是同一个」。
/// </para>
/// <para>
/// 它对内核**不透明**:<c>Game</c> 存这个字符串、不读它,与斗地主的发牌完全同理。
/// </para>
/// </summary>
public sealed record XiangqiSetup(string Board, int FirstSeat)
{
    /// <summary>盘面串的长度 —— 10 行 × 9 列。</summary>
    public const int BoardLength = XiangqiBoard.RowCount * XiangqiBoard.ColCount;

    /// <summary>棋子代码 → 类型。大小写决定哪一方,所以这里只列大写。</summary>
    private static readonly IReadOnlyDictionary<char, XiangqiPieceType> Codes =
        new Dictionary<char, XiangqiPieceType>
        {
            ['R'] = XiangqiPieceType.Chariot,
            ['N'] = XiangqiPieceType.Horse,
            ['B'] = XiangqiPieceType.Elephant,
            ['A'] = XiangqiPieceType.Advisor,
            ['K'] = XiangqiPieceType.General,
            ['C'] = XiangqiPieceType.Cannon,
            ['P'] = XiangqiPieceType.Soldier,
        };

    /// <summary>编码成随本局存下的那个字符串。</summary>
    /// <returns>不透明的设置字符串。</returns>
    public string Encode() => $"{Board}:{FirstSeat}";

    /// <summary>
    /// 解码并**校验**。任何一条不满足都抛,并说明是哪一条。
    /// <para>
    /// **不合法时 MUST NOT 退回标准开局** —— 那会让一个坏设置表现成「这局怎么是开局」,
    /// 而那和一局正常的棋在界面上完全一样。
    /// </para>
    /// </summary>
    /// <param name="setup">设置字符串。</param>
    /// <param name="seatCount">本棋种的座位数,用来校验先走方。</param>
    /// <returns>解出来的设置。</returns>
    /// <exception cref="InvalidGameSetupException">任何一条不满足。</exception>
    public static XiangqiSetup Decode(string setup, int seatCount)
    {
        var parts = (setup ?? string.Empty).Split(':');
        if (parts.Length != 2)
        {
            throw new InvalidGameSetupException(
                "Setup must be '<board>:<firstSeat>'; got " +
                $"{parts.Length} colon-separated part(s).");
        }

        var board = parts[0];
        if (board.Length != BoardLength)
        {
            // 长度错一个字符会让 `row * 9 + col` 之后的每一行都错开一列 —— 画出来处处是错,
            // 而看起来完全正常。所以它是**第一条**检查。
            throw new InvalidGameSetupException(
                $"Board must be exactly {BoardLength} characters; got {board.Length}.");
        }

        if (!int.TryParse(parts[1], out var firstSeat) || firstSeat < 0 || firstSeat >= seatCount)
        {
            throw new InvalidGameSetupException(
                $"First seat must be an integer in [0, {seatCount}); got '{parts[1]}'.");
        }

        var reds = 0;
        var blacks = 0;
        for (var i = 0; i < BoardLength; i++)
        {
            var code = board[i];
            if (code == '.') continue;

            var upper = char.ToUpperInvariant(code);
            if (!Codes.TryGetValue(upper, out var type))
            {
                throw new InvalidGameSetupException(
                    $"Unknown piece code '{code}' at index {i} (row {i / XiangqiBoard.ColCount}, " +
                    $"col {i % XiangqiBoard.ColCount}).");
            }

            var red = code == upper;
            var row = i / XiangqiBoard.ColCount;
            var col = i % XiangqiBoard.ColCount;

            if (type == XiangqiPieceType.General)
            {
                if (red) reds++; else blacks++;
                RequirePalace(red, row, col, "general");
            }
            else if (type == XiangqiPieceType.Advisor)
            {
                RequirePalace(red, row, col, "advisor");
            }
        }

        // 恰好一帅一将 —— 少一个的话「将死」判不出来,多一个的话判出来的是哪个都不对。
        if (reds != 1 || blacks != 1)
        {
            throw new InvalidGameSetupException(
                $"A position needs exactly one general per side; found {reds} red and {blacks} black.");
        }

        return new XiangqiSetup(board, firstSeat);
    }

    /// <summary>把盘面串摆成一块局面。**调用方拿到的是新的一块**,规则改不到设置。</summary>
    /// <returns>起始局面。</returns>
    internal XiangqiBoard ToBoard()
    {
        var board = XiangqiBoard.Empty();
        for (var i = 0; i < BoardLength; i++)
        {
            var code = Board[i];
            if (code == '.') continue;
            var upper = char.ToUpperInvariant(code);
            // 红是 Stone.Black —— 本棋种里「先手方」就是红,见 XiangqiRules 的类注释。
            var side = code == upper ? Stone.Black : Stone.White;
            board.Set(i / XiangqiBoard.ColCount, i % XiangqiBoard.ColCount,
                new XiangqiPiece(Codes[upper], side));
        }
        return board;
    }

    /// <summary>将 / 士必须在自己那一侧的九宫内。</summary>
    private static void RequirePalace(bool red, int row, int col, string what)
    {
        var okCol = col is >= 3 and <= 5;
        var okRow = red ? row >= 7 : row <= 2;
        if (!okCol || !okRow)
        {
            throw new InvalidGameSetupException(
                $"The {(red ? "red" : "black")} {what} at (row {row}, col {col}) is outside its palace.");
        }
    }
}
