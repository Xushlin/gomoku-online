using Gomoku.Domain.Enums;

namespace Gomoku.Application.Common.DTOs;

/// <summary>
/// 用户战绩列表卡片。比 <see cref="GameReplayDto"/> 精简:不含 Host(和 Black 同一个人)、
/// 不含 Moves 数组(列表视图太重);点进去再拉 <c>/api/rooms/{id}/replay</c>。
/// </summary>
public sealed record UserGameSummaryDto(
    Guid RoomId,
    string Name,
    UserSummaryDto Black,
    UserSummaryDto White,
    DateTime StartedAt,
    DateTime EndedAt,
    GameResult Result,
    Guid? WinnerUserId,
    GameEndReason EndReason,
    int MoveCount);
