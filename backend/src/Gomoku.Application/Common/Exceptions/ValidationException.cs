using FluentValidation.Results;

namespace Gomoku.Application.Common.Exceptions;

/// <summary>
/// 应用级输入校验失败异常。由 <c>ValidationBehavior</c> 在 handler 执行前根据
/// FluentValidation 的失败结果抛出。<see cref="Errors"/> 以字段名分组,便于前端逐字段展示。
/// 全局异常中间件把它映射到 HTTP 400 + <c>ProblemDetails</c>(含 <c>errors</c> 字典)。
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>按字段名分组的错误消息集合。</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>以一组 FluentValidation 的失败结果构造。</summary>
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation errors occurred.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).ToArray());
    }
}
