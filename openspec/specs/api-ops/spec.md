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

