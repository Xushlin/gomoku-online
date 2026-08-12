using Gomoku.Domain.Users;

namespace Gomoku.Application.Abstractions;

/// <summary>签发的 Access Token 与其过期时间戳。</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAt);

/// <summary>
/// JWT Access Token 与 Refresh Token 的签发 / 哈希契约。Refresh Token 明文仅出现在
/// 响应体中,数据库只存 SHA-256 哈希。
/// </summary>
public interface IJwtTokenService
{
    /// <summary>为 <paramref name="user"/> 签发一枚 Access Token(HS256,15 分钟过期)。</summary>
    AccessToken GenerateAccessToken(User user);

    /// <summary>生成一枚高熵(≥ 256 bit) refresh token 原文(base64url)。</summary>
    string GenerateRefreshToken();

    /// <summary>对 refresh token 原文计算 SHA-256 哈希。返回值**仅**用于入库和查找。</summary>
    string HashRefreshToken(string rawToken);
}
