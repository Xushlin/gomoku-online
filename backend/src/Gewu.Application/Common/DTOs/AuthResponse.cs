namespace Gomoku.Application.Common.DTOs;

/// <summary>
/// 注册 / 登录 / 刷新成功后返回给客户端的响应体。<see cref="RefreshToken"/> 是**原始字符串**,
/// 仅此一次出现(数据库只存其 SHA-256 哈希)。客户端需自行保存此 refresh token 以备将来刷新。
/// </summary>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    UserDto User);
