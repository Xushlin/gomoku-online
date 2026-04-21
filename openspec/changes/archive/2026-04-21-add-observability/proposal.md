## Why

到目前为止,六个能力全部上线;`AiMoveWorker` / `TurnTimeoutWorker` / `MakeMoveCommandHandler` / `SignalRRoomNotifier` 都在调 `ILogger<T>.LogInformation` / `LogError`,但:

- 默认 `Microsoft.Extensions.Logging` 只写控制台纯文本,生产 / 压测 / 故障诊断时没法检索。
- 没有 **Correlation Id**:同一个 HTTP 请求里 Controller → MediatR handler → Repository → SignalR notifier 的日志散得到处都是,看不出"这一条请求引发了哪些副作用"。
- 没有 **handler 级别的 timing**:对局结束的 ELO 路径走了多少 ms、TurnTimeoutWorker 一轮扫库多少条、AI `HardAi.SelectMove` 耗时分布,全靠猜。
- 没有结构化字段:日志消息里"Room abc123 player xyz resigned"是一个字符串 —— 想按 RoomId 过滤需要文本搜索。

这一轮不是"对业务加功能"—— 是**后端基础设施**。做完之后,每条日志都是一个带字段的 JSON 对象,携带 `RequestId` / `UserId` / `RoomId` / `DurationMs`,能直接 grep / 导 ElasticSearch / Loki。再加一个 MediatR 管道行为,自动对每个 Command / Query log 开始 + 结束 + 耗时,为后续性能优化打下观测基础。

把 `Serilog` 作为日志后端(替换但兼容 `Microsoft.Extensions.Logging`,现有 `ILogger<T>` 注入点零改动)。

## What Changes

- **NuGet**(新 deps):
  - `Serilog.AspNetCore`(同时拉 `Serilog` 核心和 `Serilog.Sinks.Console`)
  - `Serilog.Sinks.File`
  - `Serilog.Enrichers.Environment`(`MachineName`、`EnvironmentName`)
- **Application**:
  - 新 `Features/Common/Behaviors/LoggingBehavior.cs`(MediatR `IPipelineBehavior<TRequest, TResponse>`):
    - 进入 handler 前 `LogInformation("Handling {RequestName}")`(带 structured `RequestName`)。
    - 成功返回 `LogInformation("Handled {RequestName} in {DurationMs} ms")`,`DurationMs` 用 `Stopwatch`。
    - 异常捕获(不 swallow)`LogError(ex, "Handler {RequestName} failed after {DurationMs} ms")`,**rethrow**。
  - 注册在 `AddApplication` 的 MediatR pipeline,在 `ValidationBehavior` **之后**(validator 不走 logging,因为 validator 未通过是预期的 400,无需每条都 log)。
- **Api**:
  - 新 `Middleware/CorrelationIdMiddleware.cs`:
    - 读取请求头 `X-Correlation-Id`(或 `Request-Id`,W3C traceparent 的 兼容备选);若缺失生成新 `Guid.NewGuid().ToString("N")[..16]`(16 字符短 id)。
    - `Serilog.Context.LogContext.PushProperty("CorrelationId", id)` —— 当前请求期间所有日志自动带这个字段。
    - 响应 header `X-Correlation-Id: {id}` 回写 —— 客户端遇到错误可以把该 id 附在 bug report 里。
  - `Program.cs`:
    - `builder.Host.UseSerilog((ctx, services, lc) => lc.ReadFrom.Configuration(ctx.Configuration).ReadFrom.Services(services).Enrich.FromLogContext().Enrich.WithMachineName().Enrich.WithEnvironmentName())`。
    - `app.UseSerilogRequestLogging()` —— 每个 HTTP 请求一条汇总日志(method / path / status / elapsed + auto 加入 CorrelationId via LogContext)。
    - 在 `UseAuthentication` 之后加 `app.UseMiddleware<CorrelationIdMiddleware>()` —— 确保 `UserId` 已从 JWT 提取;若 middleware 早于 Auth 则 `User.FindFirst("sub")?.Value` 是 null。
    - 在 CorrelationIdMiddleware 里,若 `HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)` 非空,**额外** `PushProperty("UserId", sub)` —— 所有带身份的请求的日志自动带 UserId。
  - `GomokuHub.cs`:
    - `OnConnectedAsync` / `OnDisconnectedAsync` 里 `using (LogContext.PushProperty("ConnectionId", Context.ConnectionId)) using (LogContext.PushProperty("UserId", Context.UserIdentifier))` 包裹;LogInformation("SignalR connection opened / closed")。
  - `appsettings.json` 新增 `"Serilog"` 段:
    ```
    "Serilog": {
      "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
      "MinimumLevel": {
        "Default": "Information",
        "Override": {
          "Microsoft.AspNetCore": "Warning",
          "Microsoft.EntityFrameworkCore": "Warning"
        }
      },
      "WriteTo": [
        { "Name": "Console", "Args": { "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}" } },
        { "Name": "File", "Args": { "path": "logs/gomoku-.log", "rollingInterval": "Day", "retainedFileCountLimit": 7, "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact" } }
      ]
    }
    ```
    - 依赖 `Serilog.Formatting.Compact` NuGet,但 `Serilog.AspNetCore` 已包含。
    - 注意:`logs/` 路径相对 Api 工作目录;确保 `.gitignore` 已忽略 `[Ll]og(s?)/`(现有 `.gitignore` 已含 `[Ll]ogs/`,生效)。
  - `appsettings.Development.json` 覆盖:
    - `"MinimumLevel": { "Default": "Debug" }` —— 开发观察详细。
    - File sink 关掉(只靠 Console 观察)?为了统一行为,不关,`logs/` 在 gitignore 里无压力。
- **Tests**:
  - `Gomoku.Application.Tests/Features/Common/Behaviors/LoggingBehaviorTests.cs`:
    - 成功路径:调用 next 一次,Logger 收到 2 条 Information(enter + exit),无 Error。
    - 异常路径:next 抛,Logger 收到 Information(enter) + Error(ex);异常 rethrow(不 swallow)。
    - 耗时字段:使用 `FakeTimeProvider` 或直接断言 DurationMs ≥ 0。
  - 不加 CorrelationIdMiddleware 单测(ASP.NET 中间件单测依赖 TestServer;本次 scope 小)。

**显式不做**(留给后续变更):
- OpenTelemetry 导出(OTLP / Jaeger / Azure Monitor):不在本次,留给 `add-otel-export`。
- Structured logging 级别过滤的**动态配置**(运行时切日志级别):生产配置 + 滚动文件 + 短停机足够。
- 敏感字段脱敏(password / JWT 内容):现有 handler 里就**没有**把 `request.Password` log 出来(看过 `LoginCommandHandler`)。若以后有新 handler 引入日志,要审查;本次加一行约定在 design.md 里。
- ELK / Loki 对接:是个部署动作,不动代码。
- Metrics(Prometheus):log 可看的不替代;`add-metrics-prometheus` 单独做。
- 前端日志采集:完全在前端范畴。

## Capabilities

### New Capabilities

- **`observability`** — 统一的结构化日志约定:correlation id 全链路、MediatR handler timing、SignalR connection 日志、日志级别配置与 sink 约定、敏感字段脱敏纪律。

### Modified Capabilities

(无)

## Impact

- **代码规模**:~5 新文件(CorrelationIdMiddleware + LoggingBehavior + 两个测试 + appsettings 片段)+ `Program.cs` / `GomokuHub.cs` 少量改动。
- **NuGet**:+3(`Serilog.AspNetCore` / `Serilog.Sinks.File` / `Serilog.Enrichers.Environment`)。Application 层**零**新 NuGet(`Microsoft.Extensions.Logging.Abstractions` 已在那)。
- **HTTP 表面**:零新端点。每个请求响应多一个 `X-Correlation-Id` 头。
- **SignalR 表面**:零变化。
- **数据库**:零变化。
- **运行时**:
  - 每个请求 enter/exit 各一条日志(+MediatR handler 各一条),对 throughput 影响在 ~µs 级别,忽略不计。
  - File sink 每天一个文件,保留 7 天;磁盘占用 < 100MB / 天(粗估)。
- **后续变更将依赖**:`add-otel-export`(复用 Serilog 的 structured fields)、`add-metrics-prometheus`、任何需要"按 RoomId / UserId 过滤日志"的故障诊断场景。
