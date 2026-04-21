namespace Gomoku.Api;

/// <summary>
/// CORS 策略配置 POCO,绑定 <c>appsettings.json</c> 的 <c>"Cors"</c> 段。
/// <see cref="PolicyName"/> 是整个 Api 里**唯一**允许出现的策略名字符串 —— 不得用字面量。
/// </summary>
public sealed class CorsOptions
{
    /// <summary>CORS 策略名;在 <c>Program.cs</c> 与 <c>app.UseCors</c> 引用。</summary>
    public const string PolicyName = "FrontendPolicy";

    /// <summary>
    /// 允许的跨域 origin 列表(完整 scheme+host+port)。空数组 = 完全禁止跨域(保守默认)。
    /// Production 通常用 env var <c>GOMOKU_CORS__ALLOWEDORIGINS__0=https://gomoku.example.com</c> 覆盖。
    /// </summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
