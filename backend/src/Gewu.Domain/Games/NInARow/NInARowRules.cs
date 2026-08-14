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
    /// <param name="supportsHumanVsHuman">
    /// 本棋种是否存在人类对手池。默认 <c>true</c> —— **没有**人类对手才是需要在调用处
    /// 写出理由的那一侧。
    /// </param>
    /// <param name="isRated">
    /// 本棋种是否结算 ELO。默认 <c>true</c> —— 一个棋种默认是算分的,
    /// **不**算分才是需要在调用处写出理由的那一侧。
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="gameKey"/> 为空,或违反不变量
    /// <c>isRated ⇒ supportsHumanVsHuman</c>。
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">尺寸或连子长度不合法。</exception>
    public NInARowRules(
        string gameKey,
        int rows,
        int cols,
        int winLength,
        bool supportsHumanVsHuman = true,
        bool isRated = true)
    {
        if (string.IsNullOrWhiteSpace(gameKey))
        {
            throw new ArgumentException("Game key must be non-empty.", nameof(gameKey));
        }

        // 不变量:只能跟机器人下的棋种不存在有意义的评分 —— 机器人对局是计分的,
        // 所以那种阶梯排出来的是"谁刷弱档刷得多"而不是棋力。在**构造处**失败,而不是等到
        // 某个 handler 算出一个没人该看的分数。
        if (isRated && !supportsHumanVsHuman)
        {
            throw new ArgumentException(
                $"Game '{gameKey}' cannot be rated: it has no human-vs-human mode, so its only " +
                "opponents are bots and a ladder over it would rank grinding, not skill.",
                nameof(isRated));
        }

        // 尺寸与连子长度的合法性交给 Board 判 —— 那里已经有完整的校验,
        // 在这里复制一遍就等于有了两份真源。构造一块盘顺带把参数验了。
        _ = new Board(rows, cols, winLength);

        GameKey = gameKey;
        Rows = rows;
        Cols = cols;
        WinLength = winLength;
        SupportsHumanVsHuman = supportsHumanVsHuman;
        IsRated = isRated;
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
    public bool SupportsHumanVsHuman { get; }

    /// <inheritdoc />
    public bool IsRated { get; }

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
    public static readonly IGameRules Gomoku =
        new NInARowRules(GameKeys.Gomoku, 15, 15, 5);

    /// <summary>
    /// 一字棋:3×3 连三。**没有人人对战,因此不计分。**
    /// <para>
    /// 不计分不是一个独立的选择,而是不变量的后果:平台没有为一字棋提供人人对战入口
    /// (它只有 <c>/g/tictactoe</c> 这一个人机页面),于是它唯一的对手是机器人,而机器人
    /// 对局是计分的 —— 一字棋阶梯的榜首会是刷 Easy 档最多的人。构造器会拒绝
    /// <c>supportsHumanVsHuman: false, isRated: true</c> 的组合,所以这件事不靠谁记得。
    /// </para>
    /// <para>
    /// 它将来获得人人对战时,翻 <c>supportsHumanVsHuman</c> 会把评分从"禁止"变成"允许";
    /// 开不开是那时的一个独立决定。顺带一提,即便开了,一字棋是**已解游戏**(双方稍具水平
    /// 即必和,<c>TicTacToeHardAi</c> 不可战胜),阶梯的分辨力也很有限 —— 但那时它至少
    /// 量的是人,而不是刷机器人的次数。
    /// </para>
    /// <para>
    /// 这里没有第二份判胜实现,整个棋种就是这三个数 —— 这正是 <c>NInARowRules</c>
    /// 存在的理由,也是 <c>add-game-rules-registry</c> 那句"一个类加一处注册"
    /// 第一次被真正验证。
    /// </para>
    /// </summary>
    public static readonly IGameRules TicTacToe = new NInARowRules(
        GameKeys.TicTacToe, 3, 3, 3, supportsHumanVsHuman: false, isRated: false);
}
