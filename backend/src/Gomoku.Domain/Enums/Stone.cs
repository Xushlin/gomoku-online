namespace Gomoku.Domain.Enums;

/// <summary>
/// 棋盘上一个格子的状态:空、黑子、白子。
/// 底层值 <see cref="Empty"/> 固定为 0,确保未初始化的格子自然是空的。
/// </summary>
public enum Stone
{
    /// <summary>该格没有棋子。</summary>
    Empty = 0,

    /// <summary>黑子。按五子棋惯例,黑方先手。</summary>
    Black = 1,

    /// <summary>白子。</summary>
    White = 2,
}
