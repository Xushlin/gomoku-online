namespace Gomoku.Api;

/// <summary>
/// Rate limiting 配置 POCO,绑定 <c>appsettings.json</c> 的 <c>"RateLimiting"</c> 段。
/// 两条策略:
/// <list type="bullet">
/// <item><c>Global</c>:默认应用于所有端点,保护整体流量(除非端点显式 DisableRateLimiting 或 override)。</item>
/// <item><c>AuthStrict</c>:命名策略,贴在 auth 爆破敏感端点(login / register / refresh)上,限流更严。</item>
/// </list>
/// 策略名以常量暴露,AuthController 的 <c>[EnableRateLimiting(...)]</c> 必须引用常量,禁止字面量。
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>全局默认策略名(实际上作为 `GlobalLimiter` 无需显式绑定,保留作文档约定)。</summary>
    public const string GlobalPolicyName = "global";

    /// <summary>auth 爆破敏感端点的命名策略名。</summary>
    public const string AuthStrictPolicyName = "auth-strict";

    /// <summary>全局默认速率:100 req / 60s / IP。</summary>
    public PolicyOptions Global { get; set; } = new() { PermitLimit = 100, WindowSeconds = 60 };

    /// <summary>auth 敏感端点速率:5 req / 60s / IP。</summary>
    public PolicyOptions AuthStrict { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };
}

/// <summary>单条策略的参数。</summary>
public sealed class PolicyOptions
{
    /// <summary>窗口内允许的最多请求数。</summary>
    public int PermitLimit { get; set; }

    /// <summary>窗口长度(秒)。</summary>
    public int WindowSeconds { get; set; }
}
