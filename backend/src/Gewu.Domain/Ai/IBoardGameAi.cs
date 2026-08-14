using Gewu.Domain.Entities;
using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Ai;

/// <summary>
/// 棋盘对抗棋种的 AI 决策接口,纯函数式。
/// <para>
/// 由 <c>IGomokuAi</c> 更名而来。它从来就没用到任何五子棋专属的东西 —— 入参只有一块
/// <see cref="Board"/> 和己方棋色,而 <c>Board</c> 在 <c>add-game-rules-registry</c> 之后
/// 已经带着自己的尺寸。名字是当时唯一把它绑在一个棋种上的东西。
/// </para>
/// <list type="bullet">
/// <item>返回的 <see cref="Position"/> MUST 落在 <paramref name="board"/> 的空格上(<see cref="Stone.Empty"/>);</item>
/// <item>MUST NOT 修改入参 <paramref name="board"/>(实现内部如需试走,应先 <see cref="Board.Clone"/>);</item>
/// <item>MUST NOT 读取时钟 / 磁盘 / 网络 / 静态可变状态;</item>
/// <item>对相同 <paramref name="board"/> 快照 + 相同 <paramref name="myStone"/> 与相同随机源,输出 MUST 可复现。</item>
/// </list>
/// </summary>
public interface IBoardGameAi
{
    /// <summary>
    /// 在给定棋盘快照上,为落子方 <paramref name="myStone"/> 选择下一步。
    /// </summary>
    /// <param name="board">当前棋盘快照;调用前后内容 MUST 一致。</param>
    /// <param name="myStone">己方棋色,MUST 是 <see cref="Stone.Black"/> 或 <see cref="Stone.White"/>。</param>
    /// <returns>一个合法的空格坐标。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="myStone"/> 为 <see cref="Stone.Empty"/>。
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="board"/> 已被全部占据(五子棋 225 格、一字棋 9 格 —— 判定按
    /// <c>board.CellCount</c>,不是任何写死的数);调用方应在棋盘满之前就已结束对局。
    /// </exception>
    Position SelectMove(Board board, Stone myStone);
}

/// <summary>
/// 某个棋种的 AI 工厂:按难度构造该棋种的 AI 实例。
/// <para>
/// 形状与 <c>IGameRules</c> / <c>IPuzzleRules</c> 一致 —— 平台上"按游戏键解析实现"
/// 只该有一种写法。加一个棋种的 AI = 一个本接口实现 + 一处 DI 注册。
/// </para>
/// <para>
/// 实现 MUST 无状态:同一个实例被并发的多个房间共享。每次 <see cref="Create"/> MUST
/// 返回新的 AI 实例。
/// </para>
/// </summary>
public interface IGameAiFactory
{
    /// <summary>本工厂服务的棋种键,与规则注册表中的 key 一致。</summary>
    string GameKey { get; }

    /// <summary>
    /// 构造指定难度的 AI 实例。
    /// </summary>
    /// <param name="difficulty">AI 难度。</param>
    /// <param name="random">随机源,交由具体实现用于初始选点 / 并列打破。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="difficulty"/> 不是本棋种支持的值。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> 为 <c>null</c>。</exception>
    IBoardGameAi Create(BotDifficulty difficulty, Random random);
}

/// <summary>
/// 按棋种键解析 <see cref="IGameAiFactory"/>。未注册的键返回 <c>null</c>,
/// 由 handler 映射成 404 —— 与 <c>IGameRulesRegistry</c> 同一形状与同一处理方式。
/// </summary>
public interface IGameAiRegistry
{
    /// <summary>取指定棋种的 AI 工厂,未注册则 <c>null</c>。</summary>
    /// <param name="gameKey">棋种键。</param>
    IGameAiFactory? For(string gameKey);
}
