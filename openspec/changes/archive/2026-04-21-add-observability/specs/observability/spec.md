## ADDED Requirements

### Requirement: Serilog 作为日志后端,与 `ILogger<T>` 兼容

系统 SHALL 在 `Program.cs` 通过 `Host.UseSerilog(...)` 把 Serilog 接入 `Microsoft.Extensions.Logging.ILoggerFactory`。所有业务代码里的 `ILogger<T>` 调用(`LogInformation` / `LogError` 等)MUST 不因本次变更而修改 —— Serilog 只是替换底层写入器。

Serilog 配置 MUST 从 `appsettings.json` 的 `"Serilog"` 段读取(`ReadFrom.Configuration`),而非硬编码。允许 `appsettings.Development.json` 覆盖 MinimumLevel 为 `Debug`。

#### Scenario: 现有 ILogger 调用无缝切换
- **WHEN** 审阅 `ExceptionHandlingMiddleware` / `AiMoveWorker` / `TurnTimeoutWorker` / `SignalRRoomNotifier` / `GomokuHub` 的代码
- **THEN** 本次变更前后,`ILogger<T>` 的使用方式**完全一致**;只是输出被 Serilog 接管

#### Scenario: 启动期日志
- **WHEN** 应用刚启动、`Host.UseSerilog` 尚未完成配置的短暂阶段
- **THEN** 启动期的日志(bootstrap logger)至少输出到 Console,不会丢失

#### Scenario: 配置读取
- **WHEN** `appsettings.json` 的 `"Serilog"` 段定义了 sink / MinimumLevel / Override
- **THEN** 运行时按该配置生效;无需代码改动即可调整级别 / 关闭 File sink 等

---

### Requirement: 每 HTTP 请求由 `X-Correlation-Id` 头标识并贯穿日志

Api 层 SHALL 新增 `CorrelationIdMiddleware`,负责每个进入 HTTP 请求的 correlation id 处理:

1. 读取请求头 `X-Correlation-Id`:若非空且长度 ≤ 64 → 复用;否则生成新 `Guid.NewGuid().ToString("N").Substring(0, 16)`(16 字符 hex 短 id)。
2. 通过 `Serilog.Context.LogContext.PushProperty("CorrelationId", id)` 把 id 推入当前异步流程的日志 scope。
3. 若 `HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)` 非空,**同时** `PushProperty("UserId", sub)`。
4. 响应头 `X-Correlation-Id: {id}` 回写,让客户端 bug report / tracing 能对齐。

中间件 MUST 在 `UseAuthentication` **之后** 注册(否则 `User` claim 尚未填充,UserId 字段始终为 null)。

#### Scenario: 无 Correlation-Id 时自动生成
- **WHEN** 客户端发 HTTP 请求**不带** `X-Correlation-Id` 头
- **THEN** 响应头 MUST 含 `X-Correlation-Id: <16 hex>`;本请求期间所有日志字段 `CorrelationId` 均等于该值

#### Scenario: 客户端传入的 Correlation-Id 被复用
- **WHEN** 客户端发 HTTP 请求带 `X-Correlation-Id: abc-123-client`
- **THEN** 响应头 `X-Correlation-Id: abc-123-client`;日志 `CorrelationId` 字段也是该值(便于客户端 / 服务端日志对齐)

#### Scenario: 登录用户的日志带 UserId
- **WHEN** 带合法 JWT 的请求触发任何 handler 内部 log
- **THEN** 那些日志同时含 `CorrelationId` 与 `UserId` 两个 structured 字段

#### Scenario: 未登录请求无 UserId 字段
- **WHEN** 未带 JWT(或 JWT 无效,401 前)
- **THEN** 日志含 `CorrelationId`,**不**含 `UserId`(字段不出现,而非空字符串)

#### Scenario: 客户端传入过长 Correlation-Id 被拒
- **WHEN** 客户端传入 65+ 字符的 `X-Correlation-Id`
- **THEN** 中间件生成新的 16 字符 id(抵御恶意长字段填充日志)

---

### Requirement: `LoggingBehavior<,>` 为每个 MediatR 请求自动 log enter/exit/duration

Application 层 SHALL 在 `Gomoku.Application/Features/Common/Behaviors/LoggingBehavior.cs` 实现 `IPipelineBehavior<TRequest, TResponse>`:

- Handle 开始时 `LogInformation("Handling {RequestName}", typeof(TRequest).Name)`。
- next() 成功返回时 `LogInformation("Handled {RequestName} in {DurationMs} ms", name, stopwatch.ElapsedMilliseconds)`。
- next() 抛异常时 `LogError(ex, "Handler {RequestName} failed after {DurationMs} ms", name, stopwatch.ElapsedMilliseconds)`,然后 **rethrow**(MUST NOT swallow)。

在 `AddApplication` 的 MediatR pipeline 注册里,`LoggingBehavior` MUST 排在 `ValidationBehavior` **之后** —— 即先验证后 log,400 validation 失败不走 LoggingBehavior 的 enter 路径。

#### Scenario: 成功 Command 两条 Information
- **WHEN** 任意 handler 成功执行
- **THEN** 日志序列 MUST 包含恰好两条对应的 Information:"Handling X" → "Handled X in N ms";无 Error

#### Scenario: Handler 异常走 Error + rethrow
- **WHEN** Handler 抛 `InvalidOperationException`
- **THEN** 日志序列 MUST 包含:"Handling X" (Info) + "Handler X failed after N ms" (Error, 含 exception);调用方接收到同一个 `InvalidOperationException`(LoggingBehavior MUST NOT swallow)

#### Scenario: DurationMs 字段非负
- **WHEN** 任意 handler 执行后
- **THEN** Exit 日志的 `DurationMs` structured 字段 ≥ 0,且远小于 60 秒(sanity;不强断具体值)

#### Scenario: RequestName 是类型短名
- **WHEN** 执行 `MakeMoveCommand`
- **THEN** 日志的 `RequestName` 字段值为 `"MakeMoveCommand"`(而非完整 FQCN)

---

### Requirement: SignalR 连接的 UserId / ConnectionId 进入日志 scope

`GomokuHub` 的 `OnConnectedAsync` / `OnDisconnectedAsync` MUST 在 `LogContext.PushProperty("ConnectionId", Context.ConnectionId)` + `PushProperty("UserId", Context.UserIdentifier ?? "anonymous")` 的 scope 里 log 连接开启 / 关闭事件。

断开时若 `exception != null` 用 `LogWarning(exception, ...)`;否则 `LogInformation(...)`。

#### Scenario: 连接打开日志
- **WHEN** SignalR 客户端成功连接 `/hubs/gomoku`
- **THEN** Console / File 日志含一条 "SignalR connection opened",带 structured 字段 `ConnectionId` 与 `UserId`

#### Scenario: 异常断开 Warning
- **WHEN** 客户端连接因网络错误中断,SignalR 调 `OnDisconnectedAsync(ex)`
- **THEN** 日志级别为 Warning,含 exception stack trace + ConnectionId + UserId

#### Scenario: 正常断开 Information
- **WHEN** 客户端主动 disconnect
- **THEN** 日志级别为 Information,含 ConnectionId + UserId,**不**含 exception

---

### Requirement: 敏感字段不写入日志

任何 Handler / Middleware / Notifier / Worker 编写日志消息时,**MUST NOT** 输出下列字段的原始值(包括 template argument 与 exception message):

- 密码原文(`LoginCommand.Password`、`RegisterCommand.Password`、任何别的带 Password 的字段)
- Refresh token 原文 或其 SHA-256 hash(`RefreshTokenCommand.RefreshToken`、`AuthResponse.RefreshToken`)
- JWT access token 原文(`AuthResponse.AccessToken`)
- `User.PasswordHash`(哪怕是 debug)

允许的替代:用 `UserId`(Guid)间接标识用户。Exception stack trace 可能带字段名(例如 `InvalidCredentialsException: "Email or password is incorrect."`)不计入敏感泄露。

这是**设计纪律**,由 code review 强制,不加运行时静态扫描(scope 外)。新的 handler PR 若引入对上述字段的 log 输出 MUST 在 review 阶段被拒。

#### Scenario: 当前 handler 审计通过
- **WHEN** 审阅截至本变更时的所有 handler(截至 add-ai-opponent-hard)
- **THEN** 所有 `ILogger.Log*` 调用 MUST NOT 引用 `request.Password` / `request.RefreshToken` / `user.PasswordHash` 等敏感字段

#### Scenario: 请求体不进自动 HTTP 日志
- **WHEN** `UseSerilogRequestLogging` 输出每请求汇总
- **THEN** 日志 MUST NOT 含请求 body 内容(Serilog 默认行为,本次**不**开启 body capture)

---

### Requirement: Sink 配置 —— Console + 滚动 File(JSON)

Api 的 `appsettings.json` `"Serilog"` 段 MUST 至少配置:

- **Console sink**:`outputTemplate` 含 CorrelationId 字段以便本地阅读。
- **File sink**:
  - 路径 `logs/gomoku-.log`(按 `RollingInterval.Day`)
  - `retainedFileCountLimit: 7`(保留最近 7 天)
  - `formatter: CompactJsonFormatter`(生产 grep / 导入 ELK 可直接解析)
- **Enrichers**:`FromLogContext`(必须,否则 CorrelationId / UserId 不会出现)、`WithMachineName`、`WithEnvironmentName`、以及 `.Enrich.WithProperty("ApplicationName", "Gomoku.Api")`。
- **Minimum level**:默认 `Information`;override `Microsoft.AspNetCore` / `Microsoft.EntityFrameworkCore` 为 `Warning`。

`appsettings.Development.json` MAY override MinimumLevel 为 `Debug`。

`.gitignore` MUST 包含 `[Ll]ogs/` 模式(已存在,本次无改动)。

#### Scenario: File 日志是 JSON
- **WHEN** 应用运行后一条日志被写入
- **THEN** `logs/gomoku-<YYYYMMDD>.log` 的每一行 MUST 是合法 JSON object,至少含 `@t`(时间戳)+ `@mt`(message template)+ `@l`(level)+ `CorrelationId`(若该条日志在请求 scope 内)+ `MachineName` + `EnvironmentName`

#### Scenario: Console 日志带 CorrelationId
- **WHEN** 应用运行时观察 Console 输出
- **THEN** 请求 scope 内的日志 MUST 带 16 字符 hex `CorrelationId`,方便 `tail | grep <id>` 追踪

#### Scenario: 第 8 天的日志文件被自动清理
- **WHEN** `retainedFileCountLimit = 7`、应用已运行超过 8 天
- **THEN** 文件数 ≤ 7;最旧的被 Serilog 自动删除

---

### Requirement: Application 层不新增 Serilog 依赖

`Gomoku.Application.csproj` MUST NOT 添加对 `Serilog` / `Serilog.*` 的任何 PackageReference。LoggingBehavior 及其它需要 log 的代码都通过 `Microsoft.Extensions.Logging.Abstractions` 的 `ILogger<T>` 间接使用,保持"Application 依赖抽象,Infrastructure / Api 提供实现"的分层铁律。

只有 `Gomoku.Api.csproj` 允许 `Serilog.AspNetCore` / `Serilog.Sinks.File` / `Serilog.Enrichers.Environment` / `Serilog.Formatting.Compact` 这些包。

`Gomoku.Domain.csproj` 维持 0 PackageReference / 0 ProjectReference —— 本变更零影响。

#### Scenario: Application 依赖检查
- **WHEN** 审阅 `Gomoku.Application.csproj`
- **THEN** `<PackageReference>` 列表 MUST NOT 含 "Serilog" 前缀的包

#### Scenario: Domain 依赖检查
- **WHEN** 审阅 `Gomoku.Domain.csproj`
- **THEN** 仍 0 PackageReference / 0 ProjectReference
