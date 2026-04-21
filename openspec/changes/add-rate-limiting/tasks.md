## 1. Api — `RateLimitingOptions`

- [x] 1.1 `backend/src/Gomoku.Api/RateLimitingOptions.cs`:含两个 `PolicyOptions`(Global / AuthStrict)+ 两个常量策略名。

## 2. Api — Program.cs 接线

- [x] 2.1 `Program.cs`:
  - `using Microsoft.AspNetCore.RateLimiting;`
  - `using System.Threading.RateLimiting;`
  - 读配置:`var rlOpts = builder.Configuration.GetSection("RateLimiting").Get<RateLimitingOptions>() ?? new();`
  - `builder.Services.AddRateLimiter(options => { ... })`:
    - `options.RejectionStatusCode = 429;`
    - `options.OnRejected = async (context, ct) => { if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)) { context.HttpContext.Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds).ToString(); } await context.HttpContext.Response.WriteAsync("Too many requests.", ct); };`
    - **Global limiter**(全局默认,按 IP 分区):
      ```csharp
      options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
      {
          var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
          return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
          {
              PermitLimit = rlOpts.Global.PermitLimit,
              Window = TimeSpan.FromSeconds(rlOpts.Global.WindowSeconds),
              QueueLimit = 0,
              QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
          });
      });
      ```
    - **Named policy "auth-strict"**:`options.AddPolicy(RateLimitingOptions.AuthStrictPolicyName, ctx => { /* same 方式,用 rlOpts.AuthStrict */ });`
  - HTTP 管道:`app.UseCors(...)` 之后,`app.UseAuthentication()` 之前加 `app.UseRateLimiter()`。
  - `app.MapHealthChecks("/health").RequireRateLimiting(<empty-or-explicit-null>)` —— 正确做法是**不附加**策略;默认 global 会拦。实际:`MapHealthChecks(...).DisableRateLimiting()`(.NET 10 endpoint convention builder 扩展方法)。
  - `app.MapHub<GomokuHub>("/hubs/gomoku").DisableRateLimiting()` —— WebSocket 升级豁免。

## 3. appsettings

- [x] 3.1 `appsettings.json` 追加:
  ```json
  "RateLimiting": {
    "Global": { "PermitLimit": 100, "WindowSeconds": 60 },
    "AuthStrict": { "PermitLimit": 5, "WindowSeconds": 60 }
  }
  ```
- [x] 3.2 Development override:可选放宽(例如 Global=1000)—— 本次不加;默认够用。

## 4. Api — AuthController 贴策略

- [x] 4.1 `AuthController`:在 `Register` / `Login` / `Refresh` 三个 action 头部贴
  `[EnableRateLimiting(RateLimitingOptions.AuthStrictPolicyName)]`。
  Logout / ChangePassword 不贴 —— Logout 频率极低、ChangePassword 本身有 currentPassword 防护。

## 5. 端到端冒烟

- [x] 5.1 启动 Api。11 次快速 `POST /api/auth/login { invalid@x.com, wrongpwd }`:
  - 前 5 次 401(业务逻辑失败:邮箱不存在 / 密码错);
  - 第 6 次开始 429 + `Retry-After` 响应头为整数秒(≤ 60);
  - 等 60s 后再请求恢复。
- [x] 5.2 `GET /health` 快速 200+ 次 → 全 200,不被限流(DisableRateLimiting 生效)。
- [x] 5.3 正常频率的 `/api/rooms` 等端点不受影响(100/min 全局足够正常使用)。

## 6. 归档前置检查

- [x] 6.1 `dotnet build Gomoku.slnx`:0 警告 0 错。
- [x] 6.2 `dotnet test Gomoku.slnx`:全绿(无新测试)。
- [x] 6.3 `openspec validate add-rate-limiting --strict`:valid。
- [x] 6.4 分支 `feat/add-rate-limiting`,按层 Api / docs 两条 commit。
