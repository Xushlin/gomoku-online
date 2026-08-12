namespace Gomoku.Application.Abstractions;

/// <summary>
/// JWT 配置,在 Api 层通过 <c>appsettings.json</c> 的 <c>"Jwt"</c> 节绑定到 DI。
/// 开发环境把 <see cref="SigningKey"/> 填入 <c>appsettings.Development.json</c>(base64);
/// 生产环境通过环境变量 <c>GOMOKU_JWT__SIGNINGKEY</c> 覆盖,且启动时校验非空。
/// </summary>
public sealed class JwtOptions
{
    /// <summary>JWT <c>iss</c>。</summary>
    public string Issuer { get; set; } = "gomoku-online";

    /// <summary>JWT <c>aud</c>。</summary>
    public string Audience { get; set; } = "gomoku-online-clients";

    /// <summary>HS256 对称签名密钥,base64 编码,≥ 32 字节(解码后)。</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access Token 有效期(分钟)。默认 15。</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    /// <summary>Refresh Token 有效期(天)。默认 7。</summary>
    public int RefreshTokenLifetimeDays { get; set; } = 7;
}
