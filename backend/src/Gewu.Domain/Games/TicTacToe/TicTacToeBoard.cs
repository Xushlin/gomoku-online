using Gewu.Domain.Entities;
using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;
using DomainMove = Gewu.Domain.ValueObjects.Move;

namespace Gewu.Domain.Games.TicTacToe;

/// <summary>
/// 一字棋两个 AI 共用的盘面小工具。
/// <para>
/// 判胜**不**在这里重写 —— 一律借道 <see cref="Board.PlaceStone"/>,也就是
/// <c>NInARowRules</c> 那一套。第二份判胜实现意味着两个会各自漂移的真源,而"AI 认为
/// 这步赢了、服务端认为没赢"是最难查的一类 bug。
/// </para>
/// </summary>
internal static class TicTacToeBoard
{
    /// <summary>盘面上所有空格,阅读顺序。</summary>
    /// <param name="board">棋盘快照。</param>
    internal static List<Position> EmptyCells(Board board)
    {
        var empties = new List<Position>(board.CellCount);
        for (var r = 0; r < board.Rows; r++)
        {
            for (var c = 0; c < board.Cols; c++)
            {
                var p = new Position(r, c);
                if (board.GetStone(p) == Stone.Empty)
                {
                    empties.Add(p);
                }
            }
        }
        return empties;
    }

    /// <summary>该格是否是四个角之一。</summary>
    /// <param name="p">坐标。</param>
    internal static bool IsCorner(Position p)
        => (p.Row == 0 || p.Row == 2) && (p.Col == 0 || p.Col == 2);

    /// <summary>
    /// <paramref name="stone"/> 一手就能取胜的格,没有则 <c>null</c>。
    /// <para>
    /// 在 <see cref="Board.Clone"/> 出的副本上试走,所以入参 <paramref name="board"/>
    /// 不被修改 —— <c>IBoardGameAi</c> 的契约要求如此。
    /// </para>
    /// </summary>
    /// <param name="board">棋盘快照。</param>
    /// <param name="candidates">要试的空格。</param>
    /// <param name="stone">试走方棋色。</param>
    internal static Position? FindWinningMove(
        Board board, IReadOnlyList<Position> candidates, Stone stone)
    {
        foreach (var p in candidates)
        {
            var trial = board.Clone();
            if (IsWinFor(trial.PlaceStone(new DomainMove(p, stone)), stone))
            {
                return p;
            }
        }
        return null;
    }

    /// <summary>该结果是否表示 <paramref name="stone"/> 方获胜。</summary>
    /// <param name="result">落子后的判定结果。</param>
    /// <param name="stone">关注的一方。</param>
    internal static bool IsWinFor(GameResult result, Stone stone)
        => stone == Stone.Black ? result == GameResult.BlackWin : result == GameResult.WhiteWin;
}
