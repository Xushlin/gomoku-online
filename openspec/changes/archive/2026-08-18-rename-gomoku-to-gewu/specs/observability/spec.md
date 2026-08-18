# observability Specification Delta

## MODIFIED Requirements

### Requirement: Serilog 作为日志后端,与 `ILogger<T>` 兼容

系统 SHALL 在 `Program.cs` 通过 `Host.UseSerilog(...)` 把 Serilog 接入 `Microsoft.Extensions.Logging.ILoggerFactory`。所有业务代码里的 `ILogger<T>` 调用(`LogInformation` / `LogError` 等)MUST 不因本次变更而修改 —— Serilog 只是替换底层写入器。

Serilog 配置 MUST 从 `appsettings.json` 的 `"Serilog"` 段读取(`ReadFrom.Configuration`),而非硬编码。允许 `appsettings.Development.json` 覆盖 MinimumLevel 为 `Debug`。

#### Scenario: 现有 ILogger 调用无缝切换
- **WHEN** 审阅 `ExceptionHandlingMiddleware` / `AiMoveWorker` / `TurnTimeoutWorker` / `SignalRRoomNotifier` / `MatchHub` 的代码
- **THEN** 本次变更前后,`ILogger<T>` 的使用方式**完全一致**;只是输出被 Serilog 接管

#### Scenario: 启动期日志
- **WHEN** 应用刚启动、`Host.UseSerilog` 尚未完成配置的短暂阶段
- **THEN** 启动期的日志(bootstrap logger)至少输出到 Console,不会丢失

#### Scenario: 配置读取
- **WHEN** `appsettings.json` 的 `"Serilog"` 段定义了 sink / MinimumLevel / Override
- **THEN** 运行时按该配置生效;无需代码改动即可调整级别 / 关闭 File sink 等

### Requirement: SignalR 连接的 UserId / ConnectionId 进入日志 scope

`MatchHub` 的 `OnConnectedAsync` / `OnDisconnectedAsync` MUST 在 `LogContext.PushProperty("ConnectionId", Context.ConnectionId)` + `PushProperty("UserId", Context.UserIdentifier ?? "anonymous")` 的 scope 里 log 连接开启 / 关闭事件。

断开时若 `exception != null` 用 `LogWarning(exception, ...)`;否则 `LogInformation(...)`。

#### Scenario: 连接打开日志
- **WHEN** SignalR 客户端成功连接 `/hubs/match`
- **THEN** Console / File 日志含一条 "SignalR connection opened",带 structured 字段 `ConnectionId` 与 `UserId`

#### Scenario: 异常断开 Warning
- **WHEN** 客户端连接因网络错误中断,SignalR 调 `OnDisconnectedAsync(ex)`
- **THEN** 日志级别为 Warning,含 exception stack trace + ConnectionId + UserId

#### Scenario: 正常断开 Information
- **WHEN** 客户端主动 disconnect
- **THEN** 日志级别为 Information,含 ConnectionId + UserId,**不**含 exception

### Requirement: Sink 配置 —— Console + 滚动 File(JSON)

Api 的 `appsettings.json` `"Serilog"` 段 MUST 至少配置:

- **Console sink**:`outputTemplate` 含 CorrelationId 字段以便本地阅读。
- **File sink**:
  - 路径 `logs/gewu-.log`(按 `RollingInterval.Day`)
  - `retainedFileCountLimit: 7`(保留最近 7 天)
  - `formatter: CompactJsonFormatter`(生产 grep / 导入 ELK 可直接解析)
- **Enrichers**:`FromLogContext`(必须,否则 CorrelationId / UserId 不会出现)、`WithMachineName`、`WithEnvironmentName`、以及 `.Enrich.WithProperty("ApplicationName", "Gewu.Api")`。
- **Minimum level**:默认 `Information`;override `Microsoft.AspNetCore` / `Microsoft.EntityFrameworkCore` 为 `Warning`。

`appsettings.Development.json` MAY override MinimumLevel 为 `Debug`。

`.gitignore` MUST 包含 `[Ll]ogs/` 模式(已存在,本次无改动)。

#### Scenario: File 日志是 JSON
- **WHEN** 应用运行后一条日志被写入
- **THEN** `logs/gewu-<YYYYMMDD>.log` 的每一行 MUST 是合法 JSON object,至少含 `@t`(时间戳)+ `@mt`(message template)+ `@l`(level)+ `CorrelationId`(若该条日志在请求 scope 内)+ `MachineName` + `EnvironmentName`

#### Scenario: Console 日志带 CorrelationId
- **WHEN** 应用运行时观察 Console 输出
- **THEN** 请求 scope 内的日志 MUST 带 16 字符 hex `CorrelationId`,方便 `tail | grep <id>` 追踪

#### Scenario: 第 8 天的日志文件被自动清理
- **WHEN** `retainedFileCountLimit = 7`、应用已运行超过 8 天
- **THEN** 文件数 ≤ 7;最旧的被 Serilog 自动删除
