## Why

前端一接入 → 任何恶意脚本都能以 `curl` 循环爆破:
- `/api/auth/login` 密码字典爆破;
- `/api/auth/register` 注册账号刷战绩;
- `/api/auth/refresh` 无效 token 探测;
- `/api/users?search=` 或 `/api/users/{id}/games` 刷分页耗数据库。

补 **ASP.NET Core RateLimiter 中间件**(.NET 7+ 内置,零 NuGet)。两条策略:
1. **全局默认**:`100 req / min / IP`—— 防整体刷流。
2. **auth-strict**:`5 req / min / IP`—— 贴在 `/api/auth/login` / `/register` / `/refresh` 上,防爆破。

Health 端点 **豁免**(运维探针高频触发是正常的);SignalR Hub WebSocket upgrade 也豁免(一次握手长连接)。

## What Changes

- **NuGet**:零(RateLimiter 在 ASP.NET Core 10 框架自带)。
- **Api**:
  - 新 `RateLimitingOptions.cs`:
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
  - `appsettings.json` 新 `"RateLimiting"` 段,含默认值(上面的 100/min 与 5/min)。
  - `Program.cs`:
    - `builder.Services.AddRateLimiter(options => { ... })`:
      - 注册 `GlobalLimiter` 为**按 IP 分区**的 `FixedWindowLimiter`(用 `RateLimitPartition.GetFixedWindowLimiter` + remote IP key)。
      - 注册命名策略 `"auth-strict"`:同形但更严。
      - `OnRejected` 回调设置响应头 `Retry-After`(秒数)+ 返回 `429 Too Many Requests` + `ProblemDetails` body。
    - HTTP 管道:`app.UseRateLimiter()` 放在 `UseCors` 之后 + `UseAuthentication` 之前(匿名 / 认证都要限流)。
    - `app.MapHealthChecks("/health").DisableRateLimiting()` 以及 `/health/ready` 同样 —— 探针豁免。
    - `app.MapHub<GomokuHub>("/hubs/gomoku").DisableRateLimiting()` —— WebSocket 升级只一次,长连接内部不走 HTTP;若限流会误伤。
  - `AuthController` 的 `Register` / `Login` / `RefreshToken` actions 贴 `[EnableRateLimiting(RateLimitingOptions.AuthStrictPolicyName)]`。其他 action(`Logout` / `ChangePassword`)走 global 默认 —— logout 频次极低,change-password 本身有"当前密码"二次验证。
- **Tests**:
  - 不加单元测试(rate limiter 是 ASP.NET Core 框架测试覆盖;应用层无逻辑)。
  - E2E smoke:11 次快速 `POST /api/auth/login` → 前 5 次 401(密码错但未被限流);第 6+ 次开始返回 429 + `Retry-After` 头。全局限流:200 次 `GET /health` 仍正常(health 豁免)。

**显式不做**(留给后续):
- 按 UserId 的**认证后**分区限流(global 限流现在是 IP 维度,多用户 NAT 后共享 IP 会互相影响):下一次 `add-rate-limiting-by-user`。
- Redis-backed 限流器(水平扩展):单实例内存够用。
- SignalR Hub 方法(MakeMove / SendChat / Urge)的业务级限流("某用户 1s 内不能落子 10 次"):Domain 层已由 `NotYourTurnException` 等自然限制,更细粒度的防刷由 `add-message-throttling` 覆盖。
- Captcha / bot detection:超出 MVP。
- 按端点细分(leaderboard 比 auth 宽松)-需求驱动地再加;本次两档够。

## Capabilities

### Modified Capabilities

- **`api-ops`** — 新增 `RateLimiter` 中间件配置,两条命名策略(`global` / `auth-strict`);Auth 爆破敏感端点贴 `auth-strict`;Health / SignalR Hub 端点豁免限流。

### New Capabilities

(无)

## Impact

- **代码规模**:~5 文件(Options POCO + Program.cs 块 + appsettings + spec + tasks)。
- **NuGet**:零。
- **HTTP 表面**:所有端点响应多一种可能:429 Too Many Requests + `Retry-After` 头。现有 200 / 400 / 401 / 404 / 409 不变。
- **SignalR**:豁免,不受影响。
- **数据库**:零。
- **运行时**:rate limiter 内存字典每 IP 一个计数器,µs 级开销。
- **后续变更依赖**:`add-rate-limiting-by-user` 扩展分区策略;前端收 429 后退避重试逻辑。
