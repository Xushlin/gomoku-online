## 1. NuGet

- [x] 1.1 `backend/src/Gomoku.Api/Gomoku.Api.csproj` 新增 `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`(与 EF Core 10 匹配版本)。

## 2. Api — `CorsOptions` POCO

- [x] 2.1 `backend/src/Gomoku.Api/CorsOptions.cs`:
  ```csharp
  public sealed class CorsOptions
  {
      public const string PolicyName = "FrontendPolicy";
      public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
  }
  ```

## 3. Api — Program.cs 接线

- [x] 3.1 `Program.cs`:
  - `using Microsoft.AspNetCore.Cors.Infrastructure;`
  - 服务注册阶段:
    ```csharp
    var corsOptions = builder.Configuration.GetSection("Cors").Get<CorsOptions>() ?? new();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorsOptions.PolicyName, policy =>
        {
            policy.WithOrigins(corsOptions.AllowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials()
                  .WithExposedHeaders("X-Correlation-Id");
        });
    });
    ```
  - `builder.Services.AddHealthChecks().AddDbContextCheck<GomokuDbContext>("database", tags: new[] { "ready" });`
  - HTTP 管道顺序:`UseSerilogRequestLogging` → `UseCors(CorsOptions.PolicyName)` → `UseAuthentication` → `UseMiddleware<CorrelationIdMiddleware>` → `UseAuthorization` → …
  - 端点映射:
    ```csharp
    app.MapHealthChecks("/health"); // liveness:不检 DB
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = c => c.Tags.Contains("ready"),
    });
    ```
    (`HealthCheckOptions` 来自 `Microsoft.AspNetCore.Diagnostics.HealthChecks`;`ResponseWriter` 用默认即可。)

## 4. 配置

- [x] 4.1 `appsettings.json` 加 `"Cors"` 段:`"AllowedOrigins": [ "http://localhost:4200", "http://localhost:3000" ]`(Development 默认;前端 ng serve / CRA 常用端口)。
- [x] 4.2 `appsettings.Development.json` 不 override(默认够用)。
- [x] 4.3 Production:运维通过环境变量 `GOMOKU_CORS__ALLOWEDORIGINS__0 = https://gomoku.example.com` 覆盖。spec 里要记这一条运维指南。

## 5. 端到端冒烟

- [x] 5.1 启动 Api,`GET /health` → 200 + `{"status":"Healthy"}`(默认 ResponseWriter 文本)。
- [x] 5.2 `GET /health/ready` → 200 + DB 检查通过;断掉 DB(改 connection string 为不存在文件 / 停 sqlite 服务)后 → 503。
- [x] 5.3 CORS preflight:
  ```bash
  curl -i -X OPTIONS http://localhost:5145/api/rooms \
    -H "Origin: http://localhost:4200" \
    -H "Access-Control-Request-Method: GET"
  ```
  响应头含 `Access-Control-Allow-Origin: http://localhost:4200` + `Access-Control-Allow-Credentials: true` + `Access-Control-Allow-Methods: GET` + `Access-Control-Expose-Headers: X-Correlation-Id`。
- [x] 5.4 非白名单 origin:`Origin: http://evil.example.com` → 响应**不**带 `Access-Control-Allow-Origin` 头,浏览器会 block(curl 仍看到 body 但浏览器层面阻断)。
- [x] 5.5 SignalR hub `ws://localhost:5145/hubs/gomoku?access_token=...` 从 `http://localhost:4200` 页发起握手 —— 不做,SignalR WebSocket 的 CORS 验证由 ASP.NET Core 路由层已经处理,只要上面 preflight 通过即可。

## 6. 归档前置检查

- [x] 6.1 `dotnet build Gomoku.slnx`:0 警告 0 错。
- [x] 6.2 `dotnet test Gomoku.slnx`:全绿(Domain 230;Application 112,不变)。
- [x] 6.3 `openspec validate add-cors-and-health --strict`:valid。
- [x] 6.4 分支 `feat/add-cors-and-health`,按层分组 commit(Api / docs-openspec 两条 —— 无 Domain / Application / Infrastructure 改动,不拆更细)。
