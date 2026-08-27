using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.Xiangqi;

/// <summary>
/// 从一则古谱残局开局的中国象棋。
/// <para>
/// **它是一个独立的棋种键,而这不是省事 —— 是内核的不变量要求的。** <c>Room</c> 用**类型**
/// 判断这一局要不要设置,并且**两个方向都抛**(说要而没给、说不要却给了)。象棋「有时要、
/// 有时不要」会破坏它,而三条出路里前两条都要拒:
/// </para>
/// <list type="number">
/// <item><description>让 <see cref="XiangqiRules"/> 实现 <see cref="IDealtGameRules"/> 并让
/// <c>CreateSetup</c> 返回标准开局 —— 那个接口的文档自己写着「同一个种子 MUST 产出同一个
/// 字符串」,而一个**忽略**种子的实现正是它反对的「骗人的实现」。</description></item>
/// <item><description>把设置改成可选 —— 会同时删掉上面两个方向的检查,而它们各自都在防一个
/// 真实的错误心智模型。</description></item>
/// <item><description>**独立的键** —— 内核不变量一字不改,而「不计分」因此也是诚实的。</description></item>
/// </list>
/// <para>
/// <b>不计分,而理由与一字棋不同。</b> 一字棋不计分是因为它**必和**(已解游戏,阶梯量不出
/// 棋力);残局不计分是因为**开局就不公平** —— 有一方按构造是赢定的,那是谱主设计它的方式。
/// 给这样的局面算 ELO,是在给一个已知结局的局面发分。**两条理由不同这件事,正是那条
/// 「恰好」的不计分集合断言在第二个同类出现时该问的问题。**
/// </para>
/// <para>
/// <b>走子逻辑与 <see cref="XiangqiRules"/> 共用同一份</b> —— 它委托到
/// <see cref="XiangqiRules.ApplyOn"/>,而不是持有一份副本。副本会各自漂,而漂的表现是
/// **同一步棋在两个房间里一个合法一个不合法**,那种不一致没有任何断言会红。
/// </para>
/// <para>
/// 无状态,可安全地被并发的多个房间共享。
/// </para>
/// </summary>
public sealed class XiangqiEndgameRules : IBoardGameRules, IPositionalStartRules, IFirstSeatRules
{
    /// <inheritdoc />
    public string GameKey => GameKeys.XiangqiEndgame;

    /// <inheritdoc />
    public int Rows => XiangqiBoard.RowCount;

    /// <inheritdoc />
    public int Cols => XiangqiBoard.ColCount;

    /// <inheritdoc />
    public int SeatCount => BoardSeats.SeatCount;

    /// <summary>开放人人对战 —— 这正是本棋种存在的理由。</summary>
    public bool SupportsHumanVsHuman => true;

    /// <summary>不计分。理由见类注释:开局就不公平,而那是谱主设计它的方式。</summary>
    public bool IsRated => false;

    /// <inheritdoc />
    public void ValidateSetup(string setup) => XiangqiSetup.Decode(setup, SeatCount);

    /// <summary>
    /// 谁先走 —— **由设置说了算**,而不是「红先」这条约定。
    /// <para>
    /// 实测 1634 局残局里 **7 局是黑先走**,所以它是数据。假设红先会让那 7 局一开局就
    /// 轮错人,而表现是「我明明该走却点不动」。
    /// </para>
    /// </summary>
    /// <param name="state">开局时只有设置。</param>
    /// <returns>先走方座位。</returns>
    public int FirstSeat(MatchState state) => Setup(state).FirstSeat;

    /// <inheritdoc />
    public MoveApplication Apply(MatchState state, MoveIntent intent, int seat)
        => XiangqiRules.ApplyOn(GameKey, Setup(state).ToBoard(), state, intent, seat);

    /// <inheritdoc />
    public IReadOnlyList<MoveIntent> LegalMoves(IReadOnlyList<PlayedMove> history, Stone side)
    {
        // 这个入口只收历史,收不到设置 —— 它是给 AI 用的,而**残局暂时没有 AI**
        // (`IBoardGameAi.SelectMove` 同样只收历史,从残局出发它会按标准开局重建棋盘)。
        // 与其返回一份按标准开局算出来的、看起来像真的答案,不如说清楚做不到:
        // 一份错的合法着法表,表现是「机器人走出规则会拒绝的棋」。
        throw new NotSupportedException(
            $"'{GameKey}' cannot enumerate legal moves from a history alone: the position it " +
            "starts from lives in the game's setup, and this entry point does not receive it. " +
            "It exists for the AI, and this game has no AI until SelectMove takes a MatchState.");
    }

    /// <summary>取出这一局的设置。缺设置在这里是**内核违约** —— <c>Room</c> 保证它存在。</summary>
    private XiangqiSetup Setup(MatchState state)
        => XiangqiSetup.Decode(
            state.Setup ?? throw new InvalidOperationException(
                $"'{GameKey}' was asked about a game with no setup; Room guarantees one exists."),
            SeatCount);
}
