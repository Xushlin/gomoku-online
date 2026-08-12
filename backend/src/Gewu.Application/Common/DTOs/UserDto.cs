namespace Gomoku.Application.Common.DTOs;

/// <summary>
/// 用户信息对外 DTO。**MUST NOT** 包含 <c>PasswordHash</c> 或 refresh token 相关字段。
/// </summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string Username,
    int Rating,
    int GamesPlayed,
    int Wins,
    int Losses,
    int Draws,
    DateTime CreatedAt);
