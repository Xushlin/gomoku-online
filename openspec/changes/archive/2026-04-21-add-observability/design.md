## Context

跨 6 次迭代累积的服务端现在有:Room 聚合全链路、AI 两个后台 worker、SignalR hub 广播、ELO 应用、并发保护、解散 / 认输 / 超时 / Hard 难度 AI —— 这些流程平时 happy path 顺着跑,出问题时靠控制台纯文本根本定位不了"是谁的哪次请求在哪个 Handler 里花了多久然后哪里抛了什么"。这次变更把"观测能力"作为第一等能力引入,以后所有变更享受:

- 默认 structured 日志(Serilog JSON);
- 每请求一个 `CorrelationId` 贯穿所有层(controller → handler → repo → notifier);
- MediatR pipeline 自动 log 每个 Command / Query 的 enter + exit + duration + 异常;
- SignalR 连接的 userId / connectionId 进日志。

Serilog 是 .NET 生态近乎唯一的成熟 structured logging 选择;和 `Microsoft.Extensions.Logging.ILogger<T>` 完全兼容 —— 现有所有 `ILogger<T>` 注入点**零改动**,只是改了后端。

## Goals / Non-Goals

**Goals**:
- 任意一条日志能立刻回答"哪个请求引发了它"(CorrelationId 贯通)。
- 任意一次 MediatR 分发都有 enter/exit/duration 纪律。
- SignalR connection 生命周期可追溯。
- 生产级结构化 JSON 输出到滚动日志文件。
- 敏感字段(密码、token 原文、refresh token hash)不进日志 —— 写成一条 design 约定。

**Non-Goals**:
- OpenTelemetry export / Jaeger / Azure Monitor(`add-otel-export`)。
- Metrics(Prometheus / Grafana 仪表板)—— `add-metrics-prometheus`。
- 运行时动态调整日志级别(热切换)—— 配置文件 + 短重启即可。
- ELK / Loki 对接 —— 部署动作。
- 前端日志采集。
- 敏感字段自动脱敏(静态扫描 / 运行时 redact):暂以 design 纪律代替。

## Decisions

### D1 — Serilog 作为日志后端

原因:
- 完美兼容 `ILogger<T>`,现有所有 log 调用零代码改动。
- Structured logging 一等公民:`LogInformation("Room {RoomId} dissolved by {UserId}", roomId, userId)` 自动变成带字段的 JSON。
- Sink 生态成熟(Console / File / ES / Seq / Loki)。
- 设置简单:一个 `Host.UseSerilog()` + `appsettings.json` 段。

**考虑过但弃用**:
- 继续用默认 `Microsoft.Extensions.Logging.Console`:文本格式,不能 structured;生产不可用。
- NLog:同样能 structured,但生态 & 运维工具链不如 Serilog。
- Microsoft.Extensions.Telemetry(.NET 8+ 的内置选项):API 较新,生态不全,不如 Serilog 稳。

### D2 — CorrelationId 由中间件生成或复用客户端值

中间件逻辑:
1. 读取请求头 `X-Correlation-Id`(不区分大小写);若非空且格式合理(长度 ≤ 64)→ 复用。
2. 否则生成 `Guid.NewGuid().ToString("N")[..16]` —— 16 字符 hex,短而足够全局唯一。
3. `LogContext.PushProperty("CorrelationId", id)` 到 `IDisposable` scope,贯穿本请求。
4. 若 `HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value` 非空,**也** `PushProperty("UserId", sub)`。
5. 响应头 `X-Correlation-Id: {id}` 回写(让客户端日志能对齐)。

**顺序**:中间件必须排在 `UseAuthentication` **之后** —— 否则 `User` 尚未填充,UserId 字段永远是 null。

**考虑过但弃用**:
- W3C `traceparent` 兼容:完整 OTel 格式。`add-otel-export` 再处理。本次只要简单 `X-Correlation-Id`。

### D3 — MediatR `LoggingBehavior<,>`

放在 `Gomoku.Application/Features/Common/Behaviors/LoggingBehavior.cs`,实现 `IPipelineBehavior<TRequest, TResponse>`:

```csharp
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Handling {RequestName}", name);
        try
        {
            var response = await next();
            sw.Stop();
            _logger.LogInformation("Handled {RequestName} in {DurationMs} ms", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Handler {RequestName} failed after {DurationMs} ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
```

**注册顺序**:MediatR 的 `AddApplication` 扩展里:

```csharp
cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));   // 先跑(可能 400,不进 logging)
cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));      // 再跑(validator 通过后才 log)
```

**考虑过但弃用**:
- 把 Logging 放在 Validation **之前**:每个 400 validation 失败都写一条 Error —— 噪声很大,且 validation 失败是**可预期的用户错误**,不该走错误日志。
- 把请求 body 也 log:隐私风险(密码、token 原文)。Handler 名 + duration 已足够诊断性能,body 由客户端日志或异常 stack trace 承担。

### D4 — 敏感字段纪律(无代码,靠 review)

不在日志输出的字段(**约定**,新 handler 写 log 时要审查):

- `LoginCommand.Password` / `RegisterCommand.Password` 原文
- `RefreshTokenCommand.RefreshToken` 原文 + 其 SHA-256 hash
- `AuthResponse.AccessToken` / `RefreshToken` 原文(HTTP 日志中间件**不会**自动写 body,安全)
- `User.PasswordHash`(除非调试,永不 log)

如果将来需要按"哪个用户在 debug 什么"查日志,用 `UserId`(Guid)代替邮箱 / 用户名 / 密码原文。

本条在 spec 里写成 Requirement,review 时硬卡。

### D5 — Sink 配置

默认三项(从 `appsettings.json` 读):

1. **Console**:带 `outputTemplate` 方便本地阅读 —— `[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}`。
2. **File**:
   - 路径 `logs/gomoku-.log`(日期后缀由 Serilog 自动);
   - `rollingInterval: Day`;
   - `retainedFileCountLimit: 7`(只保留最近 7 天);
   - `formatter: CompactJsonFormatter`(生产 grep / 导入 ELK 好用)。
3. **`UseSerilogRequestLogging`** 每请求一条 "HTTP {Method} {Path} responded {Status} in {Elapsed:0.0000} ms" —— 比 Microsoft.AspNetCore 的默认请求日志更简洁。

`appsettings.Development.json` 只把 default level 调为 Debug;File sink 和 Console 不动。

### D6 — Enrich fields

全局 enricher(每条日志都带):
- `MachineName`(哪台机器)
- `EnvironmentName`(Development / Production / ...)
- `ApplicationName = "Gomoku.Api"`(静态常量)

Per-scope enricher(进入 LogContext 才带):
- `CorrelationId`(CorrelationIdMiddleware 推进)
- `UserId`(同上,若 JWT sub 存在)
- `ConnectionId`(Hub 的 OnConnected / OnDisconnected scope)

MediatR Handler 内部能显式:
- `_logger.LogInformation("Room {RoomId} dissolved", roomId.Value)` —— `RoomId` 自动成为 JSON 字段(Serilog message template 语法)。

### D7 — 不改现有 `ILogger<T>` 注入点

所有已有调用(`ExceptionHandlingMiddleware` / `AiMoveWorker` / `TurnTimeoutWorker` / `SignalRRoomNotifier`)**零改动**。Serilog 的 `UseSerilog` 替换底层 ILoggerFactory,`ILogger<T>` 语义完全不变。

这保证本变更对业务代码影响面 = 新增 2 个文件(CorrelationIdMiddleware + LoggingBehavior)+ Program.cs 的启动钩子 + appsettings 的配置段。

## Risks / Trade-offs

| 风险 | 影响 | 缓解 |
|---|---|---|
| Serilog 包引入后启动时间 +100ms | 可忽略 | N/A |
| File sink 在只读 / 权限不足的部署环境抛异常 | 启动失败 | Serilog 自身对文件失败是 fail-silent,log 到 Serilog 内部 SelfLog;若关键可加 `Serilog.Sinks.File.Args.pathFormat` 做健康检查 —— 本次默认 Serilog 自带的 fallback 即可 |
| 每请求一条 Enter/Exit + Serilog request log,日志条数 ×3 | 磁盘占用 | File sink 已限 7 天滚动;日志量估算 < 100MB / 天;超出再 override level 到 Warning |
| MediatR `LoggingBehavior` 同时记录 Worker 内部 Send(AiMoveWorker → ExecuteBotMoveCommand → MakeMoveCommand 嵌套发送)的日志 ×3 | 噪声 | 接受 —— 嵌套的 log 自然会带 CorrelationId(或 Worker 自己产生的),能追溯调用链 |
| 敏感字段意外 log 出来 | 安全事故 | Design D4 纪律 + code review;本次 spec 固化这一条 Requirement |
| `logs/` 目录污染 git | 无 | `.gitignore` 已含 `[Ll]ogs/` |

## Migration Plan

无 DB migration。

部署时新增 `logs/` 子目录(Serilog File sink 自动创建);生产环境若已有 `logs/` 会直接使用,不冲突。

## Open Questions

无。File sink 的 7 天滚动 / Debug default in Development / Information default in Production,都是业界常识选择。
