using Gewu.Application.Abstractions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;

namespace Gewu.Application.Common.Mapping;

/// <summary>
/// 造一局的**服务端侧设置** —— 或者在这个棋种不需要设置时,什么都不造。
/// <para>
/// 抽在这里而不是在两个 handler 里各写一遍,理由与 <c>MovePayload</c> 抽出来时相同:
/// 两处要执行同一条规则(「这个棋种要不要设置,要就取一个种子」),而任何一份被复制的规则
/// 迟早与另一份不一致 —— 那一天不会有人发现。这里失配的表现是"从大厅进的房间发牌了、
/// 从人机入口进的没发",而后者会在开局那一刻抛。
/// </para>
/// <para>
/// 熵在 Application 层取,不在 Domain 取:<c>ISeedProvider</c> 就在这一层,而 Domain 不该
/// 知道有一个随机源(见 <c>Room.JoinAsPlayer</c> 的 <c>setup</c> 参数)。
/// </para>
/// </summary>
internal static class MatchSetup
{
    /// <summary>
    /// 这个棋种需要设置就取一份,否则返回 <c>null</c>。
    /// <para>
    /// 不需要设置时 MUST NOT 调 <see cref="ISeedProvider.NextSeed"/> —— 一个每局都取一次
    /// 随机数却没人用的调用,会让"这个棋种有随机性吗"这个问题在读代码时得不到确定答案。
    /// </para>
    /// <para>
    /// **两种来源在这里恰好各占一支,而房间是必需的参数**:选定式棋种的设置在建房那一刻
    /// 就定了,存在 <see cref="Room.ChosenSetup"/> 上。让调用方自己去取那个字段,等于让
    /// 「忘了取」变成一个可能 —— 而忘了取的表现是房间坐满却开不了局,要等到几十秒后超时。
    /// </para>
    /// </summary>
    /// <param name="room">本房间 —— 选定式棋种的设置从它身上取。</param>
    /// <param name="rules">本房间棋种的规则。</param>
    /// <param name="seeds">开局种子的来源。</param>
    public static string? For(Room room, IGameRules rules, ISeedProvider seeds)
        => rules switch
        {
            IDealtGameRules dealt => dealt.CreateSetup(seeds.NextSeed()),
            IPositionalStartRules => room.ChosenSetup,
            _ => null,
        };
}
