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
/// <para>
/// 本接口除盘面属性外还承载两个**平台能力**声明(<see cref="SupportsHumanVsHuman"/>、
/// <see cref="IsRated"/>)。严格说它们不是"规则",放在这里只因为本接口就是 Domain 里
/// "按棋种注册的那个东西",为两个布尔另开一个注册表更贵。**门槛:这类能力声明超过三个时,
/// 应抽成独立的 <c>GameCapabilities</c> 类型,让本接口回到只描述盘面。**
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
    /// 本棋种是否存在**人类对手池** —— 平台有没有为它提供人人对战入口。
    /// <para>
    /// 这是一个**结构性事实**,不是判断。它与 <see cref="IsRated"/> 分开,是因为判断会过期
    /// 而事实不会:见 <see cref="IsRated"/> 上的说明。
    /// </para>
    /// <para>
    /// "本棋种有没有 AI"**不在这里声明** —— 那个问题由 <c>IGameAiRegistry.For(gameKey)</c>
    /// 是否解析出工厂回答。再加一个 <c>SupportsAi</c> 字段就是第二份真源,而两份真源迟早
    /// 不一致、且不一致的那天不会有人发现。人机与人人是两个独立的声明,所以一个棋种可以
    /// 只有其中之一(中国象棋大概会先只有人人对战)。
    /// </para>
    /// </summary>
    bool SupportsHumanVsHuman { get; }

    /// <summary>
    /// 本棋种的对局结束时是否结算 ELO。
    /// <para>
    /// **不变量:本属性为 <c>true</c> 时 <see cref="SupportsHumanVsHuman"/> 必须也为
    /// <c>true</c>。** 由 <c>NInARowRules</c> 构造器与一条遍历注册表的测试双重强制。
    /// 一个只能跟机器人下的棋种不存在有意义的评分:机器人对局是计分的(见 add-ai-opponent
    /// D7 的反套利理由),所以那种阶梯排出来的是"谁刷弱档刷得多",不是棋力。
    /// </para>
    /// <para>
    /// **这条注释此前是错的,值得说明改了什么。** 原文写的是「本字段是限期脚手架,唯一作用是
    /// 让第二个棋种不污染共享排行榜,<c>add-per-game-rating</c> MUST 删除它」。那个判断漏了
    /// 一件事:一字棋没有人人对战。池子分开之后"污染"的理由消失,但"没有有意义的对手池"
    /// 这个理由还在 —— 所以本字段不该被那个变更删掉。
    /// </para>
    /// <para>
    /// 真正的教训是形状:一个语义为"要不要算分"的手工布尔是**判断**,而判断会过期且不报错
    /// —— 一字棋将来有了人人对战,得有人**记得**回来翻它。所以它现在受不变量约束:
    /// 翻 <see cref="SupportsHumanVsHuman"/> 会把评分从"禁止"变成"允许",开不开则是一个
    /// 独立的、需要理由的决定,而不是一件依赖记性的事。**注释里的待办事项不是机制。**
    /// </para>
    /// <para>
    /// 拆除条件:本棋种获得人人对战之后,这个开关对它就不再有约束力。
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
