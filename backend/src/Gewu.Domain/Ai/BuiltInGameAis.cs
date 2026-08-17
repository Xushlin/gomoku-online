using Gewu.Domain.Games.TicTacToe;
using Gewu.Domain.Games.Xiangqi;

namespace Gewu.Domain.Ai;

/// <summary>
/// **全部内置 AI 工厂,唯一的一份清单。** DI 注册与「遍历注册表」的测试都从这里取。
/// <para>
/// 它是 <see cref="Games.NInARow.BuiltInGameRules.All"/> 的对侧,加它的理由**一字不差**地
/// 相同,而这已经是同一个缺陷第三次出现:
/// </para>
/// <list type="number">
/// <item><c>add-xiangqi</c> 删掉了 <c>AllBuiltInRules()</c> —— 它在注释里自称遍历注册表,
/// 数据源却是手写的 <c>{ Gomoku, TicTacToe }</c>,于是象棋会静静绕过它本该守住的不变量。
/// 那次的修法就是造出 <c>BuiltInGameRules.All</c> 这份唯一清单。</item>
/// <item><c>enforce-human-vs-human</c> 发现隔壁的 <c>GomokuRules.Registry</c> 同样手写成
/// <c>{ Gomoku, TicTacToe }</c>,注释同样写着「与生产 DI 一致」。**造出机制不等于采用机制** ——
/// 上一次的修复没有回头把这个夹具接到新清单上。</item>
/// <item>现在:<c>GomokuRules.AiRegistry</c>,注释还是「与生产 DI 一致」,内容还是两项,
/// 而生产 DI 从 <c>add-xiangqi-ai</c> 起就注册了三个。整个 <c>Gewu.Application.Tests</c>
/// 因此在一个「象棋没有 AI」的世界里跑 —— 而本变更正要新增一条「没有 AI 的棋种不许开 AI 房」
/// 的遍历断言。用那个夹具写,它会把错误答案钉死成规范。</item>
/// </list>
/// <para>
/// 新增一个棋种的 AI = 一个 <see cref="IGameAiFactory"/> 实现 + 往本清单加一项。DI 与测试
/// 同时跟上,没有第二处要记得改。
/// </para>
/// </summary>
public static class BuiltInGameAis
{
    /// <summary>全部内置 AI 工厂。</summary>
    public static IReadOnlyList<IGameAiFactory> All =>
        [new GomokuAiFactory(), new TicTacToeAiFactory(), new XiangqiAiFactory()];
}
