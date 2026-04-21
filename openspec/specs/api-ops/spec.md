# api-ops Specification

## Purpose

部署 / 运维层面的横切能力:CORS 策略(让前端能跨域调用)、Health check 端点(k8s / Docker 探针)、
未来的 rate limiting。不含业务语义,只关心"能不能跑、能不能被访问、能不能被保护"。

CORS:`FrontendPolicy` 从 `appsettings.json` 的 `"Cors:AllowedOrigins"` 数组读白名单;策略含
`AllowCredentials` 以兼容 SignalR WebSocket 握手,含 `WithExposedHeaders("X-Correlation-Id")` 让前端
读到 `observability` 能力设的追溯 id。保守默认(配置缺失时数组为空)= 禁止一切跨域。

Health:`/health` 是 liveness,纯 200 不检 DB;`/health/ready` 是 readiness,通过
`AddDbContextCheck` 带一次 DB ping(tag `"ready"`)。两者都无 `[Authorize]`,运维探针可直访。

实现位于 `backend/src/Gomoku.Api/CorsOptions.cs`(策略名常量 + 白名单 POCO)、
`Program.cs`(服务注册 + 中间件顺序:`UseCors` 排在 `UseAuthentication` 之前)、
`appsettings.json` 的 `"Cors"` 段。
## Requirements
### Requirement: CORS 策略从配置读取允许的 origin 列表

Api 层 SHALL 注册名为 `FrontendPolicy` 的 CORS 策略,允许的 origin 来自 `appsettings.json` 的 `"Cors:AllowedOrigins"` 数组(空数组视为"不允许任何跨域")。策略 MUST:

- `WithOrigins(allowedOrigins)` —— 不使用 `AllowAnyOrigin`(与 `AllowCredentials` 不兼容);
- `AllowAnyMethod()` —— 放行 GET / POST / PUT / DELETE / PATCH / OPTIONS;
- `AllowAnyHeader()` —— 放行 `Authorization` / `Content-Type` / `X-Correlation-Id` 等业务所需头;
- `AllowCredentials()` —— SignalR WebSocket 握手必需,同时让未来若切到 HttpOnly cookie 方案时零改代码;
- `WithExposedHeaders("X-Correlation-Id")` —— 前端 `fetch` 默认只能读 CORS-safelisted 响应头,需要显式 expose `X-Correlation-Id`(由 `observability` 能力设置)以便前端日志上报时携带。

HTTP 管道中 `app.UseCors(CorsOptions.PolicyName)` MUST 排在 `UseAuthentication` **之前** —— 预检 OPTIONS 请求不带 Authorization,必须先过 CORS。

`CorsOptions` MUST 定义 `public const string PolicyName = "FrontendPolicy"` 常量;`Program.cs` 与任何将来的策略引用都用该常量,禁止字面量重复。

#### Scenario: Preflight 放行白名单 origin
- **WHEN** 客户端发 `OPTIONS /api/rooms` 请求,`Origin: http://localhost:4200` 且该 origin 在 `Cors:AllowedOrigins`
- **THEN** 响应 204 或 200;响应头含 `Access-Control-Allow-Origin: http://localhost:4200`、`Access-Control-Allow-Credentials: true`、`Access-Control-Allow-Methods: GET, POST, PUT, DELETE, PATCH`、`Access-Control-Expose-Headers: X-Correlation-Id`

#### Scenario: Preflight 拒绝非白名单 origin
- **WHEN** `Origin: http://evil.example.com`(不在白名单)
- **THEN** 响应**不**含 `Access-Control-Allow-Origin` 头 —— 浏览器根据此判断 block

#### Scenario: Production 通过环境变量覆盖
- **WHEN** 环境变量 `GOMOKU_CORS__ALLOWEDORIGINS__0 = https://gomoku.example.com`
- **THEN** 运行时 CORS 白名单含该 origin(.NET 配置的数组覆盖语法)

#### Scenario: CORS 与 SignalR 兼容
- **WHEN** 前端从白名单 origin 发 WebSocket 握手到 `/hubs/gomoku?access_token=...`
- **THEN** 握手成功;CORS 中间件不拦 WebSocket upgrade 请求

---

### Requirement: `/health` 端点作为 liveness probe

Api 层 SHALL 在 `/health` 暴露健康检查端点,要求:

- **无 `[Authorize]`**(探针不能带 token)。
- **不**包含 DB 检查 —— liveness 只回答"进程还活着否",DB 抖动不应导致容器被重启。
- 返回 `200 OK + "Healthy"` 文本或默认 JSON;`HealthCheckOptions.Predicate` 未设置等价于"空检查集合合并通过"。

k8s / Docker 会用此端点做 livenessProbe。

#### Scenario: 进程正常时返回 Healthy
- **WHEN** Api 正常运行,`GET /health`
- **THEN** HTTP 200;响应体 `"Healthy"`

#### Scenario: 进程 hung / 崩溃时探针失败
- **WHEN** Api 进程假死(死锁 / OOM)
- **THEN** 探针超时;外部编排器按配置重启容器 —— 超出本能力范围,但端点 MUST 不主动返回 4xx/5xx 让外部误判

---

### Requirement: `/health/ready` 端点作为 readiness probe,包含 DB 连通性

Api 层 SHALL 在 `/health/ready` 暴露**包含 DB 检查**的健康检查端点:

- 服务注册:`AddHealthChecks().AddDbContextCheck<GomokuDbContext>("database", tags: new[] { "ready" })`。
- 端点映射:`MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") })`。
- 无 `[Authorize]`。

DB 不可达(文件缺失 / 锁死)时返回 503;k8s readinessProbe 据此从服务负载均衡中摘除本实例。

#### Scenario: DB 正常
- **WHEN** `GomokuDbContext` 能执行轻量探测查询
- **THEN** HTTP 200 + `"Healthy"`;响应描述可选含 `checks: [ { name: "database", status: "Healthy" } ]`

#### Scenario: DB 不可达
- **WHEN** SQLite 文件被删 / connection string 指向无效路径
- **THEN** HTTP 503,响应指出 `database` 检查失败

#### Scenario: readiness 不含 non-ready 检查
- **WHEN** 未来若有别的 health check 但不打 `"ready"` tag
- **THEN** `/health/ready` 不包含该 check;`/health` 也不包含(liveness 默认空)

---

### Requirement: `CorsOptions` 配置段绑定

Api 层 SHALL 定义 `CorsOptions { string[] AllowedOrigins }` 并绑定 `appsettings.json` 的 `"Cors"` 段。字段:

- `AllowedOrigins: string[]` —— 数组,每项是完整 origin(含 scheme + host + port),例如 `"http://localhost:4200"`。空数组合法(等于"完全不允许跨域",适合后端只被同源前端调用的部署)。

`"Cors"` 段缺失时,`CorsOptions` 采用默认空数组,所有跨域请求被 block(保守默认)。

#### Scenario: appsettings 含默认 origin 列表
- **WHEN** `appsettings.json` 有 `"Cors": { "AllowedOrigins": [ "http://localhost:4200", "http://localhost:3000" ] }`
- **THEN** 启动时 `CorsOptions.AllowedOrigins` 有两项;CORS 策略对这两个 origin 放行

#### Scenario: 配置段缺失时保守拒绝
- **WHEN** `appsettings.json` **不含** `"Cors"` 段
- **THEN** `CorsOptions.AllowedOrigins == []`;所有跨域请求 preflight 失败 —— 比意外放行所有 origin 安全

---

### Requirement: 不覆盖现有端点的 `[Authorize]` 要求

新增的 `/health` 与 `/health/ready` 端点 MUST 不加 `[Authorize]` —— 运维探针无法携带 JWT。现有所有 `/api/*` 端点的 authorization 约束 MUST 保持不变。

#### Scenario: health 端点无需 JWT
- **WHEN** 不带 Bearer token 请求 `/health` 或 `/health/ready`
- **THEN** HTTP 200(正常)或 503(DB 故障),**不**返回 401

#### Scenario: 业务端点仍要求认证
- **WHEN** 不带 token 请求 `/api/rooms` 或任何 `[Authorize]` 端点
- **THEN** HTTP 401(与本变更前行为一致)

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

