using System.IdentityModel.Tokens.Jwt;
using Gomoku.Api.Middleware;
using Gomoku.Application;
using Gomoku.Application.Abstractions;
using Gomoku.Infrastructure;
using Gomoku.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

// 关闭 ASP.NET 对 JWT claims 的"友好重命名"——保持 sub/preferred_username 原样,
// 而不是被改写成 ClaimTypes.NameIdentifier 之类的 xmlsoap URL。
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);

// ---------- 服务注册 ----------
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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
            };
        });
    builder.Services.AddAuthorization();
}

var app = builder.Build();

// ---------- HTTP pipeline ----------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 开发环境自动 migrate,避免"克隆后先 ef update"的摩擦。
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GomokuDbContext>();
    db.Database.Migrate();
}

app.Run();
