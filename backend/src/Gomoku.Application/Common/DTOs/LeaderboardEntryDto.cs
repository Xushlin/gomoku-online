namespace Gomoku.Application.Common.DTOs;

/// <summary>
/// 排行榜单条目。仅包含**公开展示**字段;MUST NOT 泄漏 <c>Email</c> / <c>PasswordHash</c>
/// / refresh token 等敏感信息。<c>Rank</c> 由 handler 按排序后下标从 1 起分配,不做并列名次处理
/// (并列展示是前端职责)。
/// </summary>
public sealed record LeaderboardEntryDto(
    int Rank,
    Guid UserId,
    string Username,
    int Rating,
    int GamesPlayed,
    int Wins,
    int Losses,
    int Draws);
