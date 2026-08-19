using Gewu.Application.Abstractions;
using Gewu.Domain.Games.Abstractions;

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
    /// 这个棋种需要设置就造一份,否则返回 <c>null</c>。
    /// <para>
    /// 不需要设置时 MUST NOT 调 <see cref="ISeedProvider.NextSeed"/> —— 一个每局都取一次
    /// 随机数却没人用的调用,会让"这个棋种有随机性吗"这个问题在读代码时得不到确定答案。
    /// </para>
    /// </summary>
    /// <param name="rules">本房间棋种的规则。</param>
    /// <param name="seeds">开局种子的来源。</param>
    public static string? For(IGameRules rules, ISeedProvider seeds)
        => rules is IDealtGameRules dealt ? dealt.CreateSetup(seeds.NextSeed()) : null;
}
