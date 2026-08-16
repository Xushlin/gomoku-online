using Gewu.Domain.Enums;

namespace Gewu.Domain.ValueObjects;

/// <summary>
/// 一个玩家想走的一步:从 <see cref="From"/> 到 <see cref="To"/>。
/// <para>
/// <see cref="From"/> 为 <c>null</c> 表示**落子类**棋种的一步(五子棋 / 一字棋:只有落点);
/// 非 <c>null</c> 表示**走子类**棋种的一步(中国象棋:从哪儿到哪儿)。
/// </para>
/// <para>
/// **MUST NOT 用一个合法值表示「没有起点」**(比如让落子类的 <c>From == To</c>)。那样读代码的人
/// 看到 <c>from == to</c> 得猜这是原地不动还是落子;<c>null</c> 说的是实话。
/// </para>
/// <para>
/// 形状对不对由**规则**校验,不由聚合根:落子类棋种收到非 <c>null</c> 的 <see cref="From"/> 会抛
/// <c>InvalidMoveException</c>,走子类收到 <c>null</c> 同样抛。聚合根不知道哪些棋种走子。
/// </para>
/// </summary>
/// <param name="From">起点;落子类棋种为 <c>null</c>。</param>
/// <param name="To">终点 / 落点。</param>
public readonly record struct MoveIntent(Position? From, Position To)
{
    /// <summary>落子类棋种的一步 —— 只有落点。</summary>
    /// <param name="to">落点。</param>
    public static MoveIntent Place(Position to) => new(null, to);

    /// <summary>走子类棋种的一步。</summary>
    /// <param name="from">起点。</param>
    /// <param name="to">终点。</param>
    public static MoveIntent Slide(Position from, Position to) => new(from, to);
}

/// <summary>
/// 已经走过的一步,构成 <c>IGameRules.Apply</c> 收到的历史。语义与 <see cref="MoveIntent"/> 一致,
/// 多一个「是哪一方走的」。
/// </summary>
/// <param name="From">起点;落子类棋种为 <c>null</c>。</param>
/// <param name="To">终点 / 落点。</param>
/// <param name="Side">走这一步的一方,<see cref="Stone.Black"/> 或 <see cref="Stone.White"/>。</param>
public readonly record struct PlayedMove(Position? From, Position To, Stone Side);

/// <summary>
/// <c>IGameRules.Apply</c> 的结果:这一步走完之后对局处于什么状态。
/// <para>
/// 只有 <see cref="Result"/> 一个字段。**不带 <c>EndReason</c>** —— 「怎么结束的」有三类
/// (规则判出 / 认输 / 超时),而规则只可能是第一类,让它每次都回一个恒定值是噪声。
/// 另外两类由 <c>Room</c> 的另外两条路径各自写入。
/// </para>
/// </summary>
/// <param name="Result">走完之后的对局状态。</param>
public readonly record struct MoveApplication(GameResult Result);
