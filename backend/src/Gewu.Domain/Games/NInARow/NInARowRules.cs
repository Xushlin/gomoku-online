using Gewu.Domain.Entities;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.NInARow;

/// <summary>
/// 「在 R×C 棋盘上先连 N 子者胜」—— 这一族棋种的通用规则。
/// <para>
/// 五子棋是 (15, 15, 5),一字棋是 (3, 3, 3)。**判胜算法一字不差**,只有三个数不同,
/// 所以不为后者另写一份实现:那等于复制一个算法只为了改两个常量。
/// </para>
/// <para>
/// 无状态,可安全地被并发的多个房间共享。
/// </para>
/// </summary>
public sealed class NInARowRules : IGameRules
{
    /// <summary>
    /// 构造一个连 N 子棋种。
    /// </summary>
    /// <param name="gameKey">棋种键,非空。</param>
    /// <param name="rows">行数,必须为正。</param>
    /// <param name="cols">列数,必须为正。</param>
    /// <param name="winLength">连子长度,必须为正且不超过 <c>max(rows, cols)</c>。</param>
    /// <exception cref="ArgumentException"><paramref name="gameKey"/> 为空。</exception>
    /// <exception cref="ArgumentOutOfRangeException">尺寸或连子长度不合法。</exception>
    public NInARowRules(string gameKey, int rows, int cols, int winLength)
    {
        if (string.IsNullOrWhiteSpace(gameKey))
        {
            throw new ArgumentException("Game key must be non-empty.", nameof(gameKey));
        }

        // 尺寸与连子长度的合法性交给 Board 判 —— 那里已经有完整的校验,
        // 在这里复制一遍就等于有了两份真源。构造一块盘顺带把参数验了。
        _ = new Board(rows, cols, winLength);

        GameKey = gameKey;
        Rows = rows;
        Cols = cols;
        WinLength = winLength;
    }

    /// <inheritdoc />
    public string GameKey { get; }

    /// <inheritdoc />
    public int Rows { get; }

    /// <inheritdoc />
    public int Cols { get; }

    /// <inheritdoc />
    public int WinLength { get; }

    /// <inheritdoc />
    public Board CreateBoard() => new(Rows, Cols, WinLength);

    /// <inheritdoc />
    public bool IsInBounds(Position position)
        => position.Row < Rows && position.Col < Cols;
}

/// <summary>平台内置棋种的规则常量。</summary>
public static class BuiltInGameRules
{
    /// <summary>五子棋:15×15 连五。与本变更前写死的常量完全一致。</summary>
    public static readonly IGameRules Gomoku = new NInARowRules("gomoku", 15, 15, 5);
}
