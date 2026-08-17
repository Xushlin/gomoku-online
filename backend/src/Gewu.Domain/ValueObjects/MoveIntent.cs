using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;

namespace Gewu.Domain.ValueObjects;

/// <summary>
/// 一个玩家想走的一步。载荷有两种,**恰好一种**被填充:
/// <list type="bullet">
/// <item><description>**位置类** —— <see cref="To"/> 非 <c>null</c>。<see cref="From"/> 为
/// <c>null</c> 表示落子(五子棋 / 一字棋只有落点),非 <c>null</c> 表示走子(中国象棋)。</description></item>
/// <item><description>**文本类** —— <see cref="Text"/> 非空白,两个位置都为 <c>null</c>。
/// 成语接龙的一步是一个成语,它没有格子。</description></item>
/// </list>
/// <para>
/// **MUST NOT 用一个合法值表示「不适用」** —— 既不能让落子类的 <c>From == To</c>,也不能让
/// 文本类的 <c>To == (0,0)</c>。读代码的人看到 <c>(0,0)</c> 得猜这是左上角还是"这一步没有格子";
/// <c>null</c> 说的是实话。
/// </para>
/// <para>
/// 「恰好一种」由**构造器**强制,不是由三个工厂函数保证。工厂是约定 —— 一个 record struct 的
/// 主构造器随时能被直接调用,而这个仓库已经反复付过「文档里写着的不变量没有机制」的账。
/// </para>
/// <para>
/// 形状对不对(这个棋种要不要 <c>From</c>、收不收文本)由**规则**校验,不由聚合根:
/// 落子类棋种收到非 <c>null</c> 的 <see cref="From"/> 会抛 <c>InvalidMoveException</c>,
/// 走子类收到 <c>null</c> 同样抛,棋盘类棋种收到 <see cref="Text"/> 也抛。聚合根不知道哪些棋种走子。
/// </para>
/// </summary>
/// <param name="From">起点;落子类与文本类为 <c>null</c>。</param>
/// <param name="To">终点 / 落点;文本类为 <c>null</c>。</param>
/// <param name="Text">这一步的文本;位置类为 <c>null</c>。</param>
public readonly record struct MoveIntent(Position? From, Position? To, string? Text)
{
    /// <summary>构造并校验「恰好一种载荷」。</summary>
    public MoveIntent()
        : this(null, null, null)
    {
    }

    /// <summary>起点;落子类与文本类为 <c>null</c>。</summary>
    public Position? From { get; } = MovePayload.Validate(From, To, Text).from;

    /// <summary>终点 / 落点;文本类为 <c>null</c>。</summary>
    public Position? To { get; } = MovePayload.Validate(From, To, Text).to;

    /// <summary>这一步的文本;位置类为 <c>null</c>。</summary>
    public string? Text { get; } = MovePayload.Validate(From, To, Text).text;

    /// <summary>落子类棋种的一步 —— 只有落点。</summary>
    /// <param name="to">落点。</param>
    public static MoveIntent Place(Position to) => new(null, to, null);

    /// <summary>走子类棋种的一步。</summary>
    /// <param name="from">起点。</param>
    /// <param name="to">终点。</param>
    public static MoveIntent Slide(Position from, Position to) => new(from, to, null);

    /// <summary>文本类棋种的一步 —— 说出一个词。</summary>
    /// <param name="text">这一步的文本,非空白。</param>
    public static MoveIntent Say(string text) => new(null, null, text);
}

/// <summary>
/// 已经走过的一步,构成 <c>IGameRules.Apply</c> 收到的历史。语义与 <see cref="MoveIntent"/> 一致,
/// 多一个「是哪一方走的」。同样强制「恰好一种载荷」。
/// </summary>
/// <param name="From">起点;落子类与文本类为 <c>null</c>。</param>
/// <param name="To">终点 / 落点;文本类为 <c>null</c>。</param>
/// <param name="Text">这一步的文本;位置类为 <c>null</c>。</param>
/// <param name="Side">走这一步的一方,<see cref="Stone.Black"/> 或 <see cref="Stone.White"/>。</param>
public readonly record struct PlayedMove(Position? From, Position? To, string? Text, Stone Side)
{
    /// <summary>起点;落子类与文本类为 <c>null</c>。</summary>
    public Position? From { get; } = MovePayload.Validate(From, To, Text).from;

    /// <summary>终点 / 落点;文本类为 <c>null</c>。</summary>
    public Position? To { get; } = MovePayload.Validate(From, To, Text).to;

    /// <summary>这一步的文本;位置类为 <c>null</c>。</summary>
    public string? Text { get; } = MovePayload.Validate(From, To, Text).text;

    /// <summary>位置类的一步。</summary>
    /// <param name="from">起点;落子类为 <c>null</c>。</param>
    /// <param name="to">终点 / 落点。</param>
    /// <param name="side">走这一步的一方。</param>
    public static PlayedMove Positional(Position? from, Position to, Stone side)
        => new(from, to, null, side);

    /// <summary>文本类的一步。</summary>
    /// <param name="text">这一步的文本。</param>
    /// <param name="side">走这一步的一方。</param>
    public static PlayedMove Said(string text, Stone side) => new(null, null, text, side);
}

/// <summary>
/// 「一步棋恰好携带一种载荷」这条不变量的**唯一**实现处。
/// <para>
/// 抽在这里而不是各写一遍,理由与它守护的东西是同一个:三处(<see cref="MoveIntent"/>、
/// <see cref="PlayedMove"/>、<c>Move</c> 实体)要执行同一条规则,而任何一份被复制的规则迟早
/// 与另一份不一致 —— 那一天不会有人发现。
/// </para>
/// </summary>
public static class MovePayload
{
    /// <summary>
    /// 校验「恰好一种载荷」并回传规范化后的三元组(文本被 trim)。
    /// </summary>
    /// <param name="from">起点。</param>
    /// <param name="to">终点 / 落点。</param>
    /// <param name="text">文本载荷。</param>
    /// <returns>规范化后的载荷。</returns>
    /// <exception cref="InvalidMoveException">两种载荷都给、都不给,或文本为空白。</exception>
    public static (Position? from, Position? to, string? text) Validate(
        Position? from, Position? to, string? text)
    {
        var hasText = text is not null;
        if (hasText && string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidMoveException("A textual move cannot be blank.");
        }

        if (to is not null && hasText)
        {
            throw new InvalidMoveException(
                "A move carries a position or a text, never both.");
        }

        if (to is null && !hasText)
        {
            throw new InvalidMoveException(
                "A move must carry either a position or a text.");
        }

        if (hasText && from is not null)
        {
            throw new InvalidMoveException("A textual move cannot have an origin.");
        }

        return (from, to, hasText ? text!.Trim() : null);
    }

    /// <summary>
    /// 取出位置类载荷,若这一步是文本类则拒绝。
    /// <para>
    /// 棋盘类规则调它作为**第一件事**。这是「形状对不对由规则校验」那条约定在有了第二种载荷
    /// 之后的具体样子:聚合根仍然不知道哪些棋种走在格子上,而一个成语落到五子棋规则里,
    /// 会得到一句说得清的拒绝,而不是一个 <c>NullReferenceException</c>。
    /// </para>
    /// </summary>
    /// <param name="intent">这一步。</param>
    /// <exception cref="InvalidMoveException">这一步是文本类。</exception>
    public static Position RequirePosition(this MoveIntent intent)
        => intent.To ?? throw new InvalidMoveException(
            "This game is played on a board; a move must name a square.");

    /// <summary>取出已走一步的位置类载荷。见 <see cref="RequirePosition(MoveIntent)"/>。</summary>
    /// <param name="move">已走的一步。</param>
    /// <exception cref="InvalidMoveException">这一步是文本类。</exception>
    public static Position RequirePosition(this PlayedMove move)
        => move.To ?? throw new InvalidMoveException(
            "This game is played on a board; a move must name a square.");

    /// <summary>
    /// 取出文本类载荷,若这一步是位置类则拒绝。文本类规则的对应入口。
    /// </summary>
    /// <param name="intent">这一步。</param>
    /// <exception cref="InvalidMoveException">这一步是位置类。</exception>
    public static string RequireText(this MoveIntent intent)
        => intent.Text ?? throw new InvalidMoveException(
            "This game is not played on a board; a move must carry a word.");

    /// <summary>取出已走一步的文本载荷。见 <see cref="RequireText(MoveIntent)"/>。</summary>
    /// <param name="move">已走的一步。</param>
    /// <exception cref="InvalidMoveException">这一步是位置类。</exception>
    public static string RequireText(this PlayedMove move)
        => move.Text ?? throw new InvalidMoveException(
            "This game is not played on a board; a move must carry a word.");
}

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
