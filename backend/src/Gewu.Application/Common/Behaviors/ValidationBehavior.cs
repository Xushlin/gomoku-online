using FluentValidation;
using Gomoku.Application.Common.Exceptions;
using MediatR;
using ValidationException = Gomoku.Application.Common.Exceptions.ValidationException;

namespace Gomoku.Application.Common.Behaviors;

/// <summary>
/// MediatR 管道行为:在 handler 执行前,依次调用 DI 容器中所有 <see cref="IValidator{TRequest}"/>,
/// 任一 validator 发现失败即抛 <see cref="ValidationException"/>(聚合所有失败的 failures)。
/// 请求类型无 validator 时直通。Handler 层 MUST NOT 手动调用 validator。
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>从 DI 注入所有针对 <typeparamref name="TRequest"/> 的 validator。</summary>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
