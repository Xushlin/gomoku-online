using Gewu.Domain.Exceptions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Api.Hubs;

/// <summary>
/// 把领域错误翻译成客户端能理解的**错误码**。
/// <para>
/// <b>这不是整洁,是一个在生产环境里关掉了的功能。</b> 一个 hub 方法抛出普通异常时,
/// 它的消息只有在 <c>EnableDetailedErrors</c> 打开时才送到客户端,而 <c>Program.cs</c>
/// 把它设成 <c>IsDevelopment()</c>。生产环境下 SignalR 会换成一句通用文案,于是客户端
/// 此前基于服务端英文散文的关键字匹配**全部落空**,每一个失败都显示「出错了,请重试」。
/// </para>
/// <para>
/// 实测过:同一次非法象棋着法,Development 显示「That move isn't allowed.」,
/// Production 显示「Something went wrong. Please try again.」。
/// </para>
/// <para>
/// <see cref="HubException"/> 的消息**无论 <c>EnableDetailedErrors</c> 如何都会送达** ——
/// 这正是这个类型存在的意义,也是为什么修法不是「在生产打开详细错误」(那会把栈和内部
/// 消息一起发给每个客户端)。
/// </para>
/// <para>
/// 负载**只有码**。玩家看到的是翻译后的文案,所以服务端英文永远不该出现在界面上;
/// 不把它发出去,这件事就做不到,而不是靠自觉不做。原始异常连同消息记进服务端日志。
/// </para>
/// </summary>
public sealed class DomainErrorHubFilter : IHubFilter
{
    /// <summary>并发冲突的码。它不是领域异常,但客户端要按它触发一次 rehydrate。</summary>
    public const string ConcurrentModificationCode = "concurrent-modification";

    private readonly ILogger<DomainErrorHubFilter> _logger;

    /// <inheritdoc />
    public DomainErrorHubFilter(ILogger<DomainErrorHubFilter> logger) => _logger = logger;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (DomainException ex)
        {
            _logger.LogInformation(
                ex,
                "Hub {Method} refused for {User}: {Code} — {Message}",
                invocationContext.HubMethodName,
                invocationContext.Context.UserIdentifier,
                ex.Code,
                ex.Message);

            throw new HubException(ex.Code);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // 不是领域错误,但客户端有一个明确的应对(重新拉一次房间状态),所以它也需要
            // 一个能被认出来的码 —— 否则它会落到 generic,而 generic 不会触发 rehydrate。
            _logger.LogInformation(
                ex,
                "Hub {Method} hit a concurrency clash for {User}.",
                invocationContext.HubMethodName,
                invocationContext.Context.UserIdentifier);

            throw new HubException(ConcurrentModificationCode);
        }

        // 其它异常**不**转换。把一个未预期的失败包装成一个客户端能理解的码,等于声称
        // 我们知道它是什么。它按既有方式冒泡 —— 生产下客户端只得到通用错误,而服务端
        // 的日志里有真正发生了什么。
    }
}
