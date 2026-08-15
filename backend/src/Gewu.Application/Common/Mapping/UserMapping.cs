using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;

namespace Gewu.Application.Common.Mapping;

/// <summary><see cref="User"/> 聚合到 <see cref="UserDto"/> 的转换。不暴露敏感字段。</summary>
public static class UserMapping
{
    /// <summary>
    /// 转换为对外 DTO。战绩四项与 Rating 取自 <paramref name="stats"/> ——
    /// 它们已不在 <see cref="User"/> 上,而是随棋种分行住在 <see cref="UserGameStats"/>。
    /// </summary>
    /// <param name="user">用户聚合。</param>
    /// <param name="stats">
    /// 该用户在**某一个**棋种上的战绩行;<c>null</c> 表示他还没在那个棋种上下完过一局,
    /// 此时用初始值(<c>Rating = 1200</c>、战绩全 0)填 DTO。
    /// <para>
    /// "存在但没下过"是一个正常答案 —— 把它变成 404 会让前端误报成"用户不存在"。
    /// </para>
    /// </param>
    public static UserDto ToDto(this User user, UserGameStats? stats) => new(
        Id: user.Id.Value,
        Email: user.Email.Value,
        Username: user.Username.Value,
        Rating: stats?.Rating ?? UserGameStats.InitialRating,
        GamesPlayed: stats?.GamesPlayed ?? 0,
        Wins: stats?.Wins ?? 0,
        Losses: stats?.Losses ?? 0,
        Draws: stats?.Draws ?? 0,
        CreatedAt: user.CreatedAt);

    /// <summary>
    /// 转换为他人可见的公开资料 DTO。<paramref name="stats"/> 的语义同
    /// <see cref="ToDto(User, UserGameStats?)"/>。
    /// </summary>
    public static UserPublicProfileDto ToPublicProfileDto(this User user, UserGameStats? stats) => new(
        Id: user.Id.Value,
        Username: user.Username.Value,
        Rating: stats?.Rating ?? UserGameStats.InitialRating,
        GamesPlayed: stats?.GamesPlayed ?? 0,
        Wins: stats?.Wins ?? 0,
        Losses: stats?.Losses ?? 0,
        Draws: stats?.Draws ?? 0,
        CreatedAt: user.CreatedAt);
}
