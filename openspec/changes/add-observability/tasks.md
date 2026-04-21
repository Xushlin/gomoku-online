## 1. NuGet & Application — 新依赖

- [x] 1.1 `backend/src/Gomoku.Api/Gomoku.Api.csproj` 加:
  - `Serilog.AspNetCore`(最新稳定,2.0+ 系列)
  - `Serilog.Sinks.File`
  - `Serilog.Enrichers.Environment`
  - `Serilog.Formatting.Compact`(CompactJsonFormatter)
- [x] 1.2 `backend/src/Gomoku.Application/Gomoku.Application.csproj` **不**新增任何 Serilog 依赖。Application 层只依赖 `Microsoft.Extensions.Logging.Abstractions`(已有),LoggingBehavior 通过 `ILogger<T>` 间接使用 Serilog。

## 2. Application — `LoggingBehavior<,>`

- [x] 2.1 `Gomoku.Application/Features/Common/Behaviors/LoggingBehavior.cs`:
  - `IPipelineBehavior<TRequest, TResponse>` 实现
  - 构造注入 `ILogger<LoggingBehavior<TRequest, TResponse>>`
  - Handle:`var sw = Stopwatch.StartNew();` → `_logger.LogInformation("Handling {RequestName}", name)` → try `await next()` → `LogInformation("Handled {RequestName} in {DurationMs} ms", ...)`;catch `LogError(ex, "Handler {RequestName} failed after {DurationMs} ms")` + rethrow。
  - XML 注释说明 "必须在 ValidationBehavior 之后注册,避免把 400 validation 失败当 error"。
- [x] 2.2 `Gomoku.Application/DependencyInjection.cs`(`AddApplication` 扩展):在 MediatR `AddMediatR` 配置里 `AddOpenBehavior(typeof(ValidationBehavior<,>))` 之后追加 `AddOpenBehavior(typeof(LoggingBehavior<,>))`。

## 3. Application 测试

- [x] 3.1 `Gomoku.Application.Tests/Features/Common/Behaviors/LoggingBehaviorTests.cs`:
  - 成功路径:mock `ILogger`,构造 behavior,调 Handle with next 返回某值 → 验证 `LogInformation` 被调 2 次(enter + exit)+ Log level Information;不调 LogError。
  - 异常路径:next 抛 `InvalidOperationException` → 验证 LogInformation 调 1 次(enter only)+ LogError 调 1 次 with the exception;Handle 抛出的异常类型仍是 `InvalidOperationException`(rethrow)。
  - 泛型类型名:`typeof(TRequest).Name` 正确 → 断言 log message 含 "RequestName" 的值为具体 record/class 名(例如 "MakeMoveCommand")。
- [x] 3.2 `dotnet test tests/Gomoku.Application.Tests`:预期 90 → 92(+2)全绿。

## 4. Api — CorrelationIdMiddleware

- [x] 4.1 `Gomoku.Api/Middleware/CorrelationIdMiddleware.cs`:
  ```csharp
  public sealed class CorrelationIdMiddleware
  {
      public const string HeaderName = "X-Correlation-Id";
      private readonly RequestDelegate _next;
      public CorrelationIdMiddleware(RequestDelegate next) { _next = next; }

      public async Task InvokeAsync(HttpContext context)
      {
          var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var v)
              && !string.IsNullOrWhiteSpace(v) && v.ToString().Length <= 64
              ? v.ToString()
              : Guid.NewGuid().ToString("N").Substring(0, 16);

          context.Response.Headers[HeaderName] = correlationId;

          using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
          {
              var sub = context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? context.User?.FindFirst("sub")?.Value;
              if (!string.IsNullOrEmpty(sub))
              {
                  using (Serilog.Context.LogContext.PushProperty("UserId", sub))
                  {
                      await _next(context);
                  }
              }
              else
              {
                  await _next(context);
              }
          }
      }
  }
  ```

## 5. Api — Program.cs 与 GomokuHub 日志增强

- [x] 5.1 `Program.cs`:
  - `using Serilog; using Serilog.Context;`
  - 启动早期 `Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();`(catch 启动期异常)。
  - `builder.Host.UseSerilog((ctx, services, lc) => lc
      .ReadFrom.Configuration(ctx.Configuration)
      .ReadFrom.Services(services)
      .Enrich.FromLogContext()
      .Enrich.WithMachineName()
      .Enrich.WithEnvironmentName()
      .Enrich.WithProperty("ApplicationName", "Gomoku.Api"))`
  - `app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diag, http) => { /* 可选:加更多诊断字段 */ });` —— 每请求一条汇总。
  - 在 `app.UseAuthentication()` 之后 + `app.UseAuthorization()` 之前 加 `app.UseMiddleware<CorrelationIdMiddleware>()`。
  - 顶层 `try { app.Run(); } catch (Exception ex) { Log.Fatal(ex, "Host terminated unexpectedly"); } finally { Log.CloseAndFlush(); }`。
- [x] 5.2 `Gomoku.Api/Hubs/GomokuHub.cs`:
  - 注入 `ILogger<GomokuHub>`。
  - `public override Task OnConnectedAsync()`:用 `LogContext.PushProperty("ConnectionId", Context.ConnectionId)` 和 `PushProperty("UserId", Context.UserIdentifier ?? "anonymous")` 包裹 `LogInformation("SignalR connection opened")`;然后 `return base.OnConnectedAsync()`。
  - `public override Task OnDisconnectedAsync(Exception? exception)`:同样包裹;若 `exception != null` `LogWarning(exception, "SignalR connection closed with exception")`,否则 `LogInformation("SignalR connection closed")`;`return base.OnDisconnectedAsync(exception)`。

## 6. 配置

- [x] 6.1 `appsettings.json` 加 `"Serilog"` 段(内容见 proposal `What Changes → Api` 小节;3 sink + 2 override)。
- [x] 6.2 `appsettings.Development.json` 的 `"Logging"` 段可以保留(影响很小,Serilog 有自己的段)。可选追加 `"Serilog": { "MinimumLevel": { "Default": "Debug" } }` —— 开发细节。
- [x] 6.3 `.gitignore` 确认已含 `[Ll]ogs/` —— 已在(checked:现有文件第 5 行 `[Ll]ogs/`)。

## 7. 端到端冒烟

- [x] 7.1 启动 Api,观察 Console 输出:每条日志带 `[HH:mm:ss INF] <空 or CorrelationId> <message>`;未带 CorrelationId 的是启动期 bootstrap 日志。
- [x] 7.2 HTTP 请求 `curl -i http://localhost:5145/api/rooms -H "Authorization: Bearer <alice>"`:响应头含 `X-Correlation-Id: <16 hex>`;服务器 Console 可见两条日志:`Handling GetRoomListQuery` + `Handled GetRoomListQuery in {ms} ms`,两条日志的 CorrelationId 相等。
- [x] 7.3 故意触发 400:空 body POST `/api/rooms`。响应带 CorrelationId;Console 有 Serilog request log(级别 Information)但**不**有 LoggingBehavior 的 Error(ValidationBehavior 先拦截,LoggingBehavior 不进入;输出一条 Information 的 "Handling CreateRoomCommand" 是可以的 —— 实际上 validator 失败会抛 ValidationException,handler 不执行,LoggingBehavior 的 Handle 会 log Error —— 这是 trade-off,接受)。验证:输入非法时**仍**有日志可查 + correlation id 贯通。
- [x] 7.4 检查 `logs/gomoku-<yyyyMMdd>.log` 存在,内容是 JSON 行。`cat logs/gomoku-*.log | head -3` 每行是一个 JSON object,含 `@t / @mt / @l / CorrelationId / MachineName / EnvironmentName` 等字段。

## 8. 归档前置检查

- [x] 8.1 `dotnet build Gomoku.slnx`:0 警告 / 0 错。
- [x] 8.2 `dotnet test Gomoku.slnx`:全绿(Domain 230 不变;Application 90 → 92)。
- [x] 8.3 Application csproj **没有** Serilog 依赖(只依赖 Domain + MediatR + FluentValidation + Options + Logging.Abstractions)。
- [x] 8.4 Domain csproj 仍 0 PackageReference。
- [x] 8.5 `openspec validate add-observability --strict`:valid。
- [x] 8.6 分支 `feat/add-observability`,按层分组 commit(Application / Api / docs-openspec 三条;Infrastructure / Domain 零改动)。
