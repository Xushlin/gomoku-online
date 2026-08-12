using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Domain.Enums;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;

namespace Gomoku.Application.Features.Rooms.Common;

/// <summary>
/// 共享的 ELO 应用 helper。`MakeMoveCommandHandler` / `ResignCommandHandler` /
/// `TurnTimeoutCommandHandler` 在对局结束路径上共用这段"加载双方 User → 推导 outcome →
/// 调 EloRating.Calculate → 各自 RecordGameResult"的 30 行逻辑,避免三遍复制。
/// <para>
/// MUST NOT 调 <c>SaveChangesAsync</c> —— 由外层 handler 合并到同一事务提交。
/// </para>
/// </summary>
internal static class GameEloApplier
{
    /// <summary>
    /// 对对局 <paramref name="room"/> 的黑 / 白方应用 ELO 变更。
    /// <paramref name="result"/> 必须是结束态之一(BlackWin / WhiteWin / Draw)。
    /// </summary>
    public static async Task ApplyAsync(
        Room room,
        GameResult result,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
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

        var (newBlackRating, newWhiteRating) = Gomoku.Domain.EloRating.EloRating.Calculate(
            black.Rating, black.GamesPlayed,
            white.Rating, white.GamesPlayed,
            outcomeForBlack);

        black.RecordGameResult(outcomeForBlack, newBlackRating);
        white.RecordGameResult(outcomeForWhite, newWhiteRating);
    }
}
