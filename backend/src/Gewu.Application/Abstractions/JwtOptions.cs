namespace Gewu.Application.Abstractions;

/// <summary>
/// JWT 配置,在 Api 层通过 <c>appsettings.json</c> 的 <c>"Jwt"</c> 节绑定到 DI。
/// 开发环境把 <see cref="SigningKey"/> 填入 <c>appsettings.Development.json</c>(base64);
/// 生产环境通过环境变量 <see cref="SigningKeyEnvironmentVariable"/> 覆盖,且启动时校验非空。
/// </summary>
public sealed class JwtOptions
{
    /// <summary>
    /// 生产环境覆盖 <see cref="SigningKey"/> 的环境变量名。
    /// <para>
    /// <b>它在这里而不是内联在启动代码里,是为了让它可以被断言。</b> `Gewu.Api` 没有测试项目
    /// (那是刻意的),所以一句写在 <c>Program.cs</c> 里的字符串没有任何东西守着 —— 而它
    /// 曾经**指着一个运行时根本不读的名字**。
    /// </para>
    /// <para>
    /// <b>实测过,两个方向都测:</b> Production 下只设 <c>GOMOKU_JWT__SIGNINGKEY</c>
    /// (启动异常此前让运维设的正是它)→ **抛出同一条异常**;只设本常量这个名字 → 正常启动、
    /// 开始监听。所以那条消息不是措辞不准,是**照着做也不管用**,而失败的样子一模一样。
    /// </para>
    /// <para>
    /// 无前缀是 .NET 的默认约定,也是 <c>api-ops</c> 那条要求明写的 MUST NOT 用
    /// <c>GOMOKU_</c> 前缀 —— <c>Program.cs</c> 从未调 <c>AddEnvironmentVariables("GOMOKU_")</c>。
    /// 双下划线是 .NET 的层级分隔符,所以本常量替换掉 <c>__</c> 之后**必须**等于配置路径
    /// <c>Jwt:SigningKey</c>,而那正是测试断言的东西 —— 断言的是两者的关系,不是把同一个
    /// 字符串再抄一遍。
    /// </para>
    /// </summary>
    public const string SigningKeyEnvironmentVariable = "Jwt__SigningKey";

    /// <summary>本配置节在 <c>appsettings.json</c> 里的名字。</summary>
    public const string SectionName = "Jwt";

    /// <summary>JWT <c>iss</c>。</summary>
    public string Issuer { get; set; } = "gewu";

    /// <summary>JWT <c>aud</c>。</summary>
    public string Audience { get; set; } = "gewu-clients";

    /// <summary>HS256 对称签名密钥,base64 编码,≥ 32 字节(解码后)。</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access Token 有效期(分钟)。默认 15。</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    /// <summary>Refresh Token 有效期(天)。默认 7。</summary>
    public int RefreshTokenLifetimeDays { get; set; } = 7;
}
