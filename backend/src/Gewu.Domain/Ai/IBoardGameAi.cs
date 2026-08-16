using Gewu.Domain.Entities;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Ai;

/// <summary>
/// 棋盘对抗棋种的 AI 决策接口,纯函数式。
/// <para>
/// 收**走子历史**、返回一个 <see cref="MoveIntent"/> —— 与 <c>IGameRules.Apply</c> 同形,
/// 理由也一样。
/// </para>
/// <para>
/// **此前的签名是 <c>SelectMove(Board, Stone) → Position</c>,而那条注释写着「它从来就没用到
/// 任何五子棋专属的东西」—— 那句话是错的。** 它有两条硬假设:吃的是 <c>Board</c>
/// (连 N 子专用的表示,带着 <c>WinLength</c> 与 <c>PlaceStone</c>),以及返回一个
/// <c>Position</c>(假设一步棋就是「落在某格」)。中国象棋两条都不满足。
/// </para>
/// <para>
/// 那句话是 <c>add-tictactoe</c> 把 <c>IGomokuAi</c> 改名成 <c>IBoardGameAi</c> 时写下的,
/// 而**一字棋证明不了它**:一字棋也是落子类、也用 <c>Board</c>。改个名字不会让一个接口变通用,
/// 加一个同族的棋种也不会 —— 这与 <c>add-tictactoe</c> 自己的审计结论(「规则花了零行」)
/// 是同一件事的两面。
/// </para>
/// <list type="bullet">
/// <item>返回的着法 MUST 在该棋种下**合法**;</item>
/// <item>MUST NOT 修改入参 <c>history</c>;</item>
/// <item>MUST NOT 读取时钟 / 磁盘 / 网络 / 静态可变状态;</item>
/// <item>相同 <c>history</c> + 相同 <c>myStone</c> + 相同随机源 → 输出 MUST 可复现。</item>
/// </list>
/// </summary>
public interface IBoardGameAi
{
    /// <summary>
    /// 在给定走子历史之后,为 <paramref name="myStone"/> 一方选择下一步。
    /// </summary>
    /// <param name="history">本局已走的全部步,按 Ply 升序;调用前后内容 MUST 一致。</param>
    /// <param name="myStone">己方,MUST 是 <see cref="Stone.Black"/> 或 <see cref="Stone.White"/>。</param>
    /// <returns>一个合法着法。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="myStone"/> 为 <see cref="Stone.Empty"/>。
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// 该方没有任何合法着法 —— 调用方应在对局结束之后就不再问 AI。
    /// </exception>
    MoveIntent SelectMove(IReadOnlyList<PlayedMove> history, Stone myStone);
}

/// <summary>
/// **落子类**棋种的 AI —— 原来的 <c>IBoardGameAi</c> 签名,原样保留。
/// <para>
/// 分出这个窄接口,是为了让既有的五个实现(五子棋 Easy / Medium / Hard、一字棋 Medium / Hard)
/// **一行不改**。它们背后有一批很值钱的测试 —— 尤其一字棋 Hard 档那套**穷举**验证:
/// 对每一个可达局面断言它落在博弈论最优值上。为了换一个签名把它们重写,
/// 是拿一份已经证明过的东西去换一次纯机械改动的风险。
/// </para>
/// </summary>
public interface IPlacementAi
{
    /// <summary>在给定棋盘快照上为己方选择落点。</summary>
    /// <param name="board">当前棋盘快照;调用前后内容 MUST 一致。</param>
    /// <param name="myStone">己方棋色。</param>
    Position SelectMove(Board board, Stone myStone);
}

/// <summary>
/// 把一个 <see cref="IPlacementAi"/> 包成 <see cref="IBoardGameAi"/>:用规则从历史重建棋盘,
/// 再把选出的落点包成一个没有起点的着法。
/// </summary>
/// <param name="inner">落子类 AI。</param>
/// <param name="rules">本棋种的规则 —— 造盘的是它,不是这里。</param>
public sealed class PlacementAiAdapter(IPlacementAi inner, INInARowRules rules) : IBoardGameAi
{
    /// <summary>
    /// 被包住的落子类 AI。公开它是因为「这个难度给的是哪个实现」是工厂的可观察行为 ——
    /// 包一层之后如果看不见,那条约束就只能靠读代码保证。
    /// </summary>
    public IPlacementAi Inner => inner;

    /// <inheritdoc />
    public MoveIntent SelectMove(IReadOnlyList<PlayedMove> history, Stone myStone)
        => MoveIntent.Place(inner.SelectMove(rules.ReplayBoard(history), myStone));
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
