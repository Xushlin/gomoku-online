using Gewu.Application.Abstractions;
using Gewu.Application.Common.Exceptions;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;

namespace Gewu.Application.Features.Rooms.Common;

/// <summary>
/// 共享的 ELO 应用 helper。`MakeMoveCommandHandler` / `ResignCommandHandler` /
/// `TurnTimeoutCommandHandler` 在对局结束路径上共用这段"取双方在该棋种上的战绩行 → 推导 outcome →
/// 调 EloRating.Calculate → 各自 RecordGameResult"的 30 行逻辑,避免三遍复制。
/// <para>
/// MUST NOT 调 <c>SaveChangesAsync</c> —— 由外层 handler 合并到同一事务提交。
/// </para>
/// <para>
/// **"不计分棋种要跳过"的判断也在这里,而不是在三个调用方各写一遍。** 对局有三条结束
/// 路径(落子成胜负 / 认输 / 超时判负),漏掉任何一条都意味着一字棋在某种结束方式下
/// 照样会动评分 —— 而"只有认输才漏"这种 bug 极难在使用中被注意到。判断放在唯一的出口上,
/// 将来加第四条结束路径也自动被覆盖。
/// </para>
/// </summary>
internal static class GameEloApplier
{
    /// <summary>
    /// 对对局 <paramref name="room"/> 的黑 / 白方应用 ELO 变更 —— 改的是双方在
    /// <c>room.GameKey</c> 这一个棋种上的 <see cref="UserGameStats"/> 行,其它棋种一个字段不动。
    /// <paramref name="result"/> 必须是结束态之一(BlackWin / WhiteWin / Draw)。
    /// <para>
    /// 棋种 <c>IsRated == false</c> 时**直接返回**:不取战绩行、不建行、不改评分。
    /// 对局本身照常结束(<c>Room.Status</c> 进 Finished、<c>EndReason</c> 已写入、
    /// 事件照常广播、回放照常可查)—— 一局棋是否算分,不影响它是否是一局棋。
    /// </para>
    /// <para>
    /// 战绩行在此**首次创建**(get-or-create):一个玩家的第一局某棋种,行还不存在。这也是
    /// 行只能在对局**结束**时出现的原因 —— 排行榜的成员资格就是"有没有这一行",提前建行会把
    /// "下过"的含义变成"点开过"。
    /// </para>
    /// </summary>
    /// <param name="room">已结束的房间。</param>
    /// <param name="result">结束态结果。</param>
    /// <param name="rules">
    /// 棋种规则注册表。解析不出 <c>room.GameKey</c> 时同样跳过计分而不是抛错:
    /// 此刻对局已经结束并记录在案,为了"算不出分"而让整个事务失败,会把一局已经下完的棋
    /// 丢掉。既然无从判断该棋种算不算分,不动评分是保守且可逆的一侧。
    /// </param>
    /// <param name="users">用户仓库。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public static async Task ApplyAsync(
        Room room,
        GameResult result,
        IGameRulesRegistry rules,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        if (rules.For(room.GameKey) is not { IsRated: true })
        {
            return;
        }

        var whiteId = room.WhitePlayerId!.Value;
        var blackStats = await users.GetOrCreateGameStatsAsync(
            room.BlackPlayerId, room.GameKey, cancellationToken);
        var whiteStats = await users.GetOrCreateGameStatsAsync(
            whiteId, room.GameKey, cancellationToken);

        var outcomeForBlack = result switch
        {
            GameResult.BlackWin => GameOutcome.Win,
            GameResult.WhiteWin => GameOutcome.Loss,
            GameResult.Draw => GameOutcome.Draw,
            _ => throw new ArgumentOutOfRangeException(
                nameof(result), result, "Unexpected GameResult for ELO."),
        };
        var outcomeForWhite = outcomeForBlack switch
        {
            GameOutcome.Win => GameOutcome.Loss,
            GameOutcome.Loss => GameOutcome.Win,
            _ => GameOutcome.Draw,
        };

        // 两个 GamesPlayed 都是**该棋种**的局数,所以 K 因子按该棋种的资历分段:一个五子棋老手
        // 第一次下象棋按 K=40 起步 —— 他在象棋上确实是新手,而那正是分棋种评分要解决的问题。
        var (newBlackRating, newWhiteRating) = Gewu.Domain.EloRating.EloRating.Calculate(
            blackStats.Rating, blackStats.GamesPlayed,
            whiteStats.Rating, whiteStats.GamesPlayed,
            outcomeForBlack);

        blackStats.RecordGameResult(outcomeForBlack, newBlackRating);
        whiteStats.RecordGameResult(outcomeForWhite, newWhiteRating);
    }
}
