using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Gomoku.Api;
using Gomoku.Api.Hubs;
using Gomoku.Api.Middleware;
using Gomoku.Application;
using Gomoku.Application.Abstractions;
using Gomoku.Infrastructure;
using Gomoku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// 关闭 ASP.NET 对 JWT claims 的"友好重命名"——保持 sub/preferred_username 原样,
// 而不是被改写成 ClaimTypes.NameIdentifier 之类的 xmlsoap URL。
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

// Bootstrap logger:在 Host.UseSerilog 完成读配置之前,启动期异常至少能写到 Console。
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Gomoku.Api host");
    await RunHostAsync(args);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

return;

static async Task RunHostAsync(string[] args)
{
var builder = WebApplication.CreateBuilder(args);

// 正式 Serilog:从配置读 sinks / MinimumLevel / Override;加 enrichers 自动带
// MachineName / EnvironmentName / ApplicationName。
builder.Host.UseSerilog((ctx, services, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("ApplicationName", "Gomoku.Api"));

// ---------- 服务注册 ----------

// CORS:给前端(Angular @ :4200 等)放行指定 origin。
// "Cors:AllowedOrigins" 段缺失时保守默认 = 完全拒绝跨域(空数组)。
// Production 通过 env var GOMOKU_CORS__ALLOWEDORIGINS__0 = https://gomoku.example.com 覆盖。
var corsOptions = builder.Configuration.GetSection("Cors").Get<CorsOptions>() ?? new CorsOptions();
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsOptions.PolicyName, policy =>
    {
        policy.WithOrigins(corsOptions.AllowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()                       // SignalR WebSocket 握手必需
              .WithExposedHeaders("X-Correlation-Id"); // 让前端 fetch 能读 observability 的 id
    });
});

// Health checks:`/health` 是 liveness(纯 200,不检 DB);
// `/health/ready` 是 readiness,带 DB ping(tags:"ready" 过滤)。
builder.Services.AddHealthChecks()
    .AddDbContextCheck<GomokuDbContext>("database", tags: new[] { "ready" });

// Rate limiting:全局 100/min/IP 兜底;auth-strict 命名策略 5/min/IP 贴 login/register/refresh。
// Health 端点 + SignalR Hub 豁免(下方 MapHealthChecks/MapHub 附加 DisableRateLimiting)。
var rlOpts = builder.Configuration.GetSection("RateLimiting").Get<RateLimitingOptions>()
             ?? new RateLimitingOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 被拒时的响应:Retry-After 头 + 纯文本 body(不含敏感信息)
    options.OnRejected = async (ctx, ct) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            ctx.HttpContext.Response.Headers["Retry-After"] =
                ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        ctx.HttpContext.Response.ContentType = "text/plain";
        await ctx.HttpContext.Response.WriteAsync("Too many requests.", ct);
    };

    // Global limiter:按 IP 分区的 FixedWindow
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rlOpts.Global.PermitLimit,
            Window = TimeSpan.FromSeconds(rlOpts.Global.WindowSeconds),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    });

    // Named "auth-strict" 策略:login / register / refresh 贴 [EnableRateLimiting("auth-strict")]
    options.AddPolicy(RateLimitingOptions.AuthStrictPolicyName, ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rlOpts.AuthStrict.PermitLimit,
            Window = TimeSpan.FromSeconds(rlOpts.AuthStrict.WindowSeconds),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    });
});

builder.Services.AddOpenApi();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 让 BotDifficulty 等枚举以 "Easy" / "Medium" 字符串形式在请求 / 响应体中出现,
        // 而不是整数;前端友好,也方便 OpenAPI 规范看。
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// SignalR + Hub 支持服务
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
}).AddJsonProtocol(options =>
{
    // 与 Controllers 的 JsonOptions 对齐:枚举以字符串出现(Stone/GameResult 等),
    // 方便客户端(包括 Flutter / Angular)按字符串解析,不必跟 C# 枚举底值耦合。
    options.PayloadSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});
builder.Services.AddSingleton<IConnectionTracker, ConnectionTracker>();
builder.Services.AddScoped<IRoomNotifier, SignalRRoomNotifier>();

// JWT Bearer 认证
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtOptions = jwtSection.Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

// D22: 生产环境 JWT 密钥非空校验 —— 避免以空密钥启动。
if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey is empty in Production. Set environment variable GOMOKU_JWT__SIGNINGKEY.");
}

if (!string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    var signingKeyBytes = Convert.FromBase64String(jwtOptions.SigningKey);
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
                ClockSkew = TimeSpan.FromSeconds(30),
                // sub claim 作为 UserIdentifier,便于 Hub 用 Clients.User(userId) 定向推送
                NameClaimType = JwtRegisteredClaimNames.Sub,
            };
            // SignalR 握手无法带 Authorization 头,允许从 ?access_token=... query 取
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var accessToken = ctx.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrEmpty(accessToken)
                        && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        ctx.Token = accessToken;
                    }
                    return Task.CompletedTask;
                },
            };
        });
    builder.Services.AddAuthorization();
}

var app = builder.Build();

// ---------- HTTP pipeline ----------
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Serilog 自带的每请求汇总日志(Method / Path / Status / Elapsed)。
// 放在中间件链靠前但在异常处理之后,能覆盖所有走完整管道的请求。
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// CORS 必须在 UseAuthentication 之前 —— 预检 OPTIONS 不带 JWT,得先过 CORS。
app.UseCors(CorsOptions.PolicyName);

// RateLimiter 在 CORS 之后、Authentication 之前:
// 让未认证的爆破请求(login/register)也能被拦;预检 OPTIONS 是安全的(GlobalLimiter 100/min 够)。
app.UseRateLimiter();

app.UseAuthentication();
// CorrelationIdMiddleware 必须在 UseAuthentication 之后 —— 否则 User.FindFirst("sub") 为 null。
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthorization();
app.MapControllers();
// SignalR 握手只发生一次;长连接内 Hub 调用走 WebSocket 帧,不重复计入 HTTP rate limit。
app.MapHub<GomokuHub>("/hubs/gomoku").DisableRateLimiting();

// Health endpoints(无 [Authorize],供运维探针;高频访问,豁免限流)
app.MapHealthChecks("/health").DisableRateLimiting(); // liveness:纯 200,不检 DB
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
}).DisableRateLimiting();

// 开发环境自动 migrate,避免"克隆后先 ef update"的摩擦。
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GomokuDbContext>();
    db.Database.Migrate();
}

await app.RunAsync();
}

