namespace Gomoku.Application.Common.DTOs;

/// <summary>
/// 他人可见的用户资料快照。比 <see cref="UserSummaryDto"/>(仅 Id + Username)多战绩 + Rating +
/// CreatedAt;比 <see cref="UserDto"/>(`/me`)少 <c>Email</c>。MUST NOT 携带任何敏感字段
/// (Email / PasswordHash / RefreshTokens / IsActive / IsBot)。
/// </summary>
public sealed record UserPublicProfileDto(
    Guid Id,
    string Username,
    int Rating,
    int GamesPlayed,
    int Wins,
    int Losses,
    int Draws,
    DateTime CreatedAt);
