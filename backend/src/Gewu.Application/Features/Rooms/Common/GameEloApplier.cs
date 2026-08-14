using Gewu.Application.Abstractions;
using Gewu.Application.Common.Exceptions;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;

namespace Gewu.Application.Features.Rooms.Common;

/// <summary>
/// 共享的 ELO 应用 helper。`MakeMoveCommandHandler` / `ResignCommandHandler` /
/// `TurnTimeoutCommandHandler` 在对局结束路径上共用这段"加载双方 User → 推导 outcome →
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
    /// 对对局 <paramref name="room"/> 的黑 / 白方应用 ELO 变更。
    /// <paramref name="result"/> 必须是结束态之一(BlackWin / WhiteWin / Draw)。
    /// <para>
    /// 棋种 <c>IsRated == false</c> 时**直接返回**:不加载 User、不改评分与战绩。
    /// 对局本身照常结束(<c>Room.Status</c> 进 Finished、<c>EndReason</c> 已写入、
    /// 事件照常广播、回放照常可查)—— 一局棋是否算分,不影响它是否是一局棋。
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

        var black = await users.FindByIdAsync(room.BlackPlayerId, cancellationToken)
            ?? throw new UserNotFoundException($"User '{room.BlackPlayerId.Value}' was not found.");
        var whiteId = room.WhitePlayerId!.Value;
        var white = await users.FindByIdAsync(whiteId, cancellationToken)
            ?? throw new UserNotFoundException($"User '{whiteId.Value}' was not found.");

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

        var (newBlackRating, newWhiteRating) = Gewu.Domain.EloRating.EloRating.Calculate(
            black.Rating, black.GamesPlayed,
            white.Rating, white.GamesPlayed,
            outcomeForBlack);

        black.RecordGameResult(outcomeForBlack, newBlackRating);
        white.RecordGameResult(outcomeForWhite, newWhiteRating);
    }
}
