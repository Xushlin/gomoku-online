using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Users;

namespace Gomoku.Application.Common.Mapping;

/// <summary><see cref="User"/> 聚合到 <see cref="UserDto"/> 的转换。不暴露敏感字段。</summary>
public static class UserMapping
{
    /// <summary>转换为对外 DTO。</summary>
    public static UserDto ToDto(this User user) => new(
        Id: user.Id.Value,
        Email: user.Email.Value,
        Username: user.Username.Value,
        Rating: user.Rating,
        GamesPlayed: user.GamesPlayed,
        Wins: user.Wins,
        Losses: user.Losses,
        Draws: user.Draws,
        CreatedAt: user.CreatedAt);
}
