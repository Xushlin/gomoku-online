using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Gomoku.Application.Common.Behaviors;

/// <summary>
/// MediatR 管道行为:为每一次 Command / Query 分发自动记录 enter / exit / duration / exception 日志。
/// 字段名使用 structured 模板:`RequestName`、`DurationMs`。配合 Serilog(由 Api 层配置)和
/// <c>CorrelationIdMiddleware</c>,每条日志自带 CorrelationId + UserId 两个 scope 字段。
/// <para>
/// **注册顺序**:在 <c>AddApplication</c> 的 MediatR pipeline 里,本 behavior MUST 排在
/// <see cref="ValidationBehavior{TRequest, TResponse}"/> **之后** —— 即先 validate 后 log。
/// 这样 validation 失败(预期的 400)不会走 LoggingBehavior 的 enter 路径,避免每条用户输入
/// 错误都写 Error 日志造成噪声。
/// </para>
/// <para>
/// 异常处理:捕获但**不 swallow**,log 后 rethrow。上游(全局异常中间件 /
/// MediatR 调用者)仍按原样处理。
/// </para>
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    /// <inheritdoc />
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Handling {RequestName}", requestName);

        try
        {
            var response = await next();
            stopwatch.Stop();
            _logger.LogInformation(
                "Handled {RequestName} in {DurationMs} ms",
                requestName,
                stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Handler {RequestName} failed after {DurationMs} ms",
                requestName,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
