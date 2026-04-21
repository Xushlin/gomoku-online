## Why

前端(Angular 21 on `http://localhost:4200` 开发,部署后换域名)要调 `http://localhost:5145/api/*`——跨域。浏览器默认 CORS 策略**直接 block**这种请求;没装 CORS middleware,任何 `fetch` / `HttpClient.get` 都失败,SignalR WebSocket 握手也挂。这是"前端能否启动开发"的硬门槛。

同样,部署要上 Docker / k8s / systemd 时,运维工具需要 `/health` 端点判活 / 判就绪。现在没有,容器起不来就没法自动重启。

这一轮装这两个基础设施:**CORS 策略**(从配置读允许的 origin)+ **Health Check 端点**(含 DB 连通性)。零业务面,两个都是"框架级 wiring"。

## What Changes

- **NuGet**(新依赖,仅 Api 层):
  - `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`(DB 就绪检查)。
- **Application**:零改动。
- **Api**:
  - `appsettings.json` 新增 `"Cors"` 段:
    ```json
    "Cors": {
      "AllowedOrigins": [ "http://localhost:4200", "http://localhost:3000" ]
    }
    ```
    (Development 默认;Production 从 env var `GOMOKU_CORS__ALLOWEDORIGINS__0` / `__1` 覆盖。)
  - `Program.cs`:
    - `builder.Services.AddCors(...)` 注册名为 `"FrontendPolicy"` 的策略:`WithOrigins(opts.AllowedOrigins)` + `AllowAnyMethod` + `AllowAnyHeader` + `AllowCredentials`(SignalR 握手需要)+ `WithExposedHeaders("X-Correlation-Id")`(前端可读 observability 的 id 上报)。
    - `builder.Services.AddHealthChecks().AddDbContextCheck<GomokuDbContext>("database")` 注册 DB 检查。
    - HTTP 管道:`app.UseCors("FrontendPolicy")` 放在 `UseAuthentication` **之前**(预检请求 OPTIONS 不带 JWT,必须先过 CORS)。
    - 端点映射:
      - `app.MapHealthChecks("/health")` —— **简单 liveness**,不做 DB 检查(避免运行时 DB 抖动导致 liveness fail 后被 k8s 误杀容器)。
      - `app.MapHealthChecks("/health/ready", options => options.Predicate = c => c.Tags.Contains("ready"))` —— readiness,包括 DB 检查(带 "ready" tag)。
    - DB check 注册时带 tag `"ready"`:`.AddDbContextCheck<GomokuDbContext>("database", tags: new[] { "ready" })`。
  - `CorsOptions` 小 POCO,绑定 `"Cors"` 段(`AllowedOrigins: string[]`)。
- **Tests**:
  - 本轮纯配置 / 中间件接线,**不**加单元测试。手工 HTTP smoke 验证 CORS preflight + /health + /health/ready 覆盖。

**显式不做**(留给后续):
- W3C `traceparent` 支持(与 `add-observability` 的 `X-Correlation-Id` 一起做成标准 distributed tracing):`add-otel-export`。
- CORS per-origin 的细粒度策略(不同 origin 不同 method 白名单):`AllowAnyMethod` 对小应用足够。
- `/health/live` 额外端点:`/health` 已经是 liveness,增加只是语义冗余。
- Health check 的丰富检查器(Redis / 外部 API / 自定义业务健康):目前后端只依赖 SQLite / DbContext,DB 是唯一关键依赖。
- CORS 预检缓存 `Access-Control-Max-Age` 调整:浏览器默认足够。

## Capabilities

### New Capabilities

- **`api-ops`** — 部署 / 运维层面的横切能力:CORS 策略、健康检查。将来 `add-rate-limiting` 也会加在这个 capability 下。

### Modified Capabilities

(无)

## Impact

- **代码规模**:~5 新 / 修改文件(Program.cs / CorsOptions.cs / appsettings / spec / tasks)。最小的一次变更。
- **NuGet**:+1(`HealthChecks.EntityFrameworkCore`)。
- **HTTP 表面**:+2 端点(`/health` / `/health/ready`,无 `[Authorize]`)。所有现有端点开始对前端可见(CORS 放行)。
- **SignalR**:**修**(WebSocket 握手经 CORS 检查;`AllowCredentials` 必须)。
- **数据库**:零变化;`/health/ready` 触发一次 `SELECT 1` 级查询。
- **运行时**:CORS 每请求 µs 级开销;`/health` 返回静态 JSON;`/health/ready` 触发 DB ping。
- **后续变更将依赖**:前端的任何跨域调用;`add-rate-limiting`(同一 `api-ops` 能力);部署配置(Docker / k8s 的 liveness/readiness probes)。
