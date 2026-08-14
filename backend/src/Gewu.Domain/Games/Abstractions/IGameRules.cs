using Gewu.Domain.Entities;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.Abstractions;

/// <summary>
/// 一个棋盘对抗棋种的盘面属性。
/// <para>
/// 规则由调用方**作为参数传入**聚合(见 <c>Room.PlayMove</c>),而不是由聚合自己去解析
/// 注册表 —— <c>Domain</c> 因此保持零外部依赖,<c>Room</c> 也仍然是其入参的纯函数,
/// 不需要一个注册表才能在测试里构造出来。
/// </para>
/// <para>
/// 实现 MUST 无状态:同一个实例会被并发的多个房间共享。任何随对局变化的字段都会
/// 变成跨房间的串味。
/// </para>
/// </summary>
public interface IGameRules
{
    /// <summary>棋种键,与房间的 <c>GameKey</c>、前端游戏注册表中的 key 一致。</summary>
    string GameKey { get; }

    /// <summary>行数。</summary>
    int Rows { get; }

    /// <summary>列数。</summary>
    int Cols { get; }

    /// <summary>判胜所需的同色连续子数。</summary>
    int WinLength { get; }

    /// <summary>
    /// 本棋种的对局结束时是否结算 ELO。
    /// <para>
    /// **这是限期存在的脚手架,不是长期设计。** 平台当前只有一个评分池,它实际上就是
    /// 五子棋排行榜;本开关唯一的作用,是让第二个棋种能在不污染那份排行榜的前提下先上线。
    /// </para>
    /// <para>
    /// <c>add-per-game-rating</c> 会给每个棋种发一份 <c>UserGameStats</c>,届时"哪个棋种
    /// 算分"不再是一个布尔,而是"每个棋种各算各的",本属性 MUST 随之删除。
    /// 那个变更的 tasks 里写着这件事 —— 加标志的变更从来不会是删标志的那个。
    /// </para>
    /// </summary>
    bool IsRated { get; }

    /// <summary>造一块本棋种的空棋盘。</summary>
    Board CreateBoard();

    /// <summary>该坐标是否在本棋种界内。<c>Position</c> 只保证非负,上界在这里判。</summary>
    /// <param name="position">坐标。</param>
    bool IsInBounds(Position position);
}

/// <summary>
/// 按棋种键解析 <see cref="IGameRules"/>。未注册的键返回 <c>null</c>,
/// 由 handler 映射成 404 —— 与 <c>IPuzzleRulesRegistry</c> 同一形状,
/// 平台上"按游戏键解析实现"只该有一种写法。
/// </summary>
public interface IGameRulesRegistry
{
    /// <summary>取指定棋种的规则,未注册则 <c>null</c>。</summary>
    /// <param name="gameKey">棋种键。</param>
    IGameRules? For(string gameKey);
}

/// <summary>
/// 平台内置棋种的键。字符串常量而非枚举 —— 新增棋种不该需要改一个共享类型;
/// 这里只是给内置棋种一个不会打错的名字。
/// </summary>
public static class GameKeys
{
    /// <summary>五子棋。</summary>
    public const string Gomoku = "gomoku";

    /// <summary>一字棋。</summary>
    public const string TicTacToe = "tictactoe";
}
