## ADDED Requirements

### Requirement: `RateLimiter` 中间件防刷

Api 层 SHALL 通过 ASP.NET Core `RateLimiter` 中间件限制每 IP 的请求速率。默认两条策略(都是 `FixedWindow`):

- **Global**(默认,未贴任何 `[EnableRateLimiting]` attribute 的端点自动应用):`PermitLimit = 100` / `Window = 60 秒`。
- **auth-strict**(命名策略,在 `[EnableRateLimiting("auth-strict")]` attribute 上启用):
  `PermitLimit = 5` / `Window = 60 秒`,用于 `/api/auth/login` / `/register` / `/refresh` 三个敏感端点。

分区键:HTTP 连接的远程 IP(`HttpContext.Connection.RemoteIpAddress`);`null` 时用字面量
`"unknown"`(不为特殊情形开后门)。

限流被拒时 MUST 返回:
- HTTP `429 Too Many Requests`;
- 响应头 `Retry-After: <seconds>`(从 `Lease.Metadata.RetryAfter` 读出);
- 响应体 `"Too many requests."`(纯文本,不含敏感信息)。

配置 MUST 从 `appsettings.json` 的 `"RateLimiting"` 段读(PermitLimit + WindowSeconds),
缺失时采用默认(100/60s 与 5/60s)。

#### Scenario: Global 限流
- **WHEN** 同一 IP 在 60s 内对非 auth-strict 端点发 101 次请求
- **THEN** 第 101 次收 HTTP 429 + `Retry-After` 头 + body "Too many requests."

#### Scenario: auth-strict 更严格
- **WHEN** 同一 IP 对 `/api/auth/login` 发第 6 次请求
- **THEN** 返回 429(即使前 5 次都是合法的业务错误如 401 密码错,也计入速率窗口)

#### Scenario: 窗口过去后重新放行
- **WHEN** 同一 IP 被限流后等 60s
- **THEN** 计数重置,新请求正常处理

#### Scenario: 不同 IP 独立计数
- **WHEN** IP A 达到限流,IP B 同时发请求
- **THEN** IP B 不受影响,各自的窗口独立

#### Scenario: 配置热加载(启动时)
- **WHEN** `appsettings.json` 配置 `"Global": { "PermitLimit": 50 }`
- **THEN** 启动后 Global 策略限制为 50/60s

---

### Requirement: Health 端点与 SignalR Hub 豁免限流

`/health` 与 `/health/ready`(由 `api-ops` 能力暴露)MUST 调用 `.DisableRateLimiting()` 豁免,
避免运维探针(k8s / docker / load balancer)高频触发限流导致误判"容器不健康"。

`/hubs/gomoku` SignalR Hub 端点 MUST 调用 `.DisableRateLimiting()` 豁免 —— WebSocket upgrade
只发生一次,长连接内部的 Hub invocation 走 WebSocket 帧,不重复计入 HTTP 限流。

#### Scenario: health 高频 probe 不被限流
- **WHEN** 同一 IP 对 `/health` 发 200 次请求(< 1 分钟)
- **THEN** 全部 HTTP 200,不出现 429

#### Scenario: SignalR 握手不计入 global 配额
- **WHEN** 同一 IP 的前端先连 `/hubs/gomoku` 再对 `/api/rooms` 发 100 次请求
- **THEN** `/api/rooms` 的第 100 次仍属于 global 的 100 条配额内(握手不占位)

---

### Requirement: `RateLimitingOptions` 配置段

Api 层 SHALL 定义 `RateLimitingOptions` POCO,绑定 `appsettings.json` 的 `"RateLimiting"` 段:

```csharp
public sealed class RateLimitingOptions
{
    public const string GlobalPolicyName = "global";
    public const string AuthStrictPolicyName = "auth-strict";
    public PolicyOptions Global { get; set; } = new() { PermitLimit = 100, WindowSeconds = 60 };
    public PolicyOptions AuthStrict { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };
}
public sealed class PolicyOptions
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}
```

`AuthStrictPolicyName` 常量 MUST 被 `AuthController` 的 `[EnableRateLimiting]` attribute 引用;
禁止散落的字符串字面量。

#### Scenario: 常量名引用统一
- **WHEN** 审阅 `AuthController.Register / Login / Refresh` 头部
- **THEN** 每个 action 都贴 `[EnableRateLimiting(RateLimitingOptions.AuthStrictPolicyName)]`(不是字面量)

#### Scenario: 缺失配置段采用默认
- **WHEN** `appsettings.json` **不含** `"RateLimiting"` 段
- **THEN** 启动采用 POCO 默认值(Global 100/60s、AuthStrict 5/60s)
