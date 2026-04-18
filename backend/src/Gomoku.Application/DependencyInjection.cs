using FluentValidation;
using Gomoku.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Gomoku.Application;

/// <summary>Application 层 DI 注册入口。Api 层通过 <c>AddApplication()</c> 一次完成接线。</summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 MediatR、FluentValidation 的所有 validator、以及 <see cref="ValidationBehavior{TRequest,TResponse}"/>。
    /// 不注册 Infrastructure 相关的 DbContext / 仓储 / 密码哈希等 —— 那些由 <c>AddInfrastructure</c> 负责。
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
