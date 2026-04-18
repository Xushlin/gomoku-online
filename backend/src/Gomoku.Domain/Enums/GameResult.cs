namespace Gomoku.Domain.Enums;

/// <summary>
/// 一盘棋的终局状态。
/// <see cref="Board.PlaceStone"/> 在每次落子后返回此枚举的一个值。
/// </summary>
public enum GameResult
{
    /// <summary>对局进行中,尚未决出胜负或平局。</summary>
    Ongoing = 0,

    /// <summary>黑方达成五连(或长连)获胜。</summary>
    BlackWin = 1,

    /// <summary>白方达成五连(或长连)获胜。</summary>
    WhiteWin = 2,

    /// <summary>棋盘已下满且无任何一方达成五连。</summary>
    Draw = 3,
}
