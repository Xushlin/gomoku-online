using System.IdentityModel.Tokens.Jwt;
using Serilog.Context;

namespace Gomoku.Api.Middleware;

/// <summary>
/// 为每个 HTTP 请求分配 Correlation Id,贯穿本次请求内所有 log scope:
/// <list type="bullet">
/// <item>读取请求头 <c>X-Correlation-Id</c>;合理(非空、长度 ≤ 64)则复用,否则生成 16 字符 hex id。</item>
/// <item>在 <see cref="LogContext"/> 推入 <c>CorrelationId</c> property,让所有本请求期间的 Serilog 日志自动带上。</item>
/// <item>若 <c>HttpContext.User</c> 含 <c>sub</c> claim(带 JWT 的请求),额外推入 <c>UserId</c>。</item>
/// <item>响应头 <c>X-Correlation-Id</c> 回写,方便客户端 bug report 对齐日志。</item>
/// </list>
/// <para>
/// 注册顺序:必须排在 <c>UseAuthentication</c> **之后** —— 否则 <c>User.FindFirst("sub")</c> 尚未被
/// JWT 中间件填充,UserId 字段永远是 null。
/// </para>
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>Correlation Id 的 HTTP header 名称。</summary>
    public const string HeaderName = "X-Correlation-Id";

    private const int MaxAcceptedLength = 64;

    private readonly RequestDelegate _next;

    /// <inheritdoc />
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ExtractOrGenerate(context);
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            var sub = context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? context.User?.FindFirst("sub")?.Value;

            if (!string.IsNullOrEmpty(sub))
            {
                using (LogContext.PushProperty("UserId", sub))
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

    private static string ExtractOrGenerate(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var raw = values.ToString();
            if (!string.IsNullOrWhiteSpace(raw) && raw.Length <= MaxAcceptedLength)
            {
                return raw;
            }
        }
        return Guid.NewGuid().ToString("N").Substring(0, 16);
    }
}
