using System.Text.RegularExpressions;

namespace Gewu.Domain.Exceptions;

/// <summary>
/// 一个被本平台**有意映射**的错误:它有身份(<see cref="Code"/>),不只是一句话。
/// <para>
/// 码是 kebab-case、全局唯一、稳定的。它是客户端唯一会看到的东西 —— hub 把领域异常
/// 转成 <c>HubException(code)</c>,负载里没有散文。消息仍然是人类散文,但它的读者是
/// 服务端日志,不是玩家。
/// </para>
/// <para>
/// <b>为什么码在这里,而不是 Api 层的一张查找表。</b> 那样会有**三**个地方逐个列举这些
/// 异常:HTTP 状态映射、hub 映射、客户端。一张表是「需要记得扩充的清单」,一个构造函数
/// 参数是「编译器不让你不给」。这个仓库已经反复付过前者的账。
/// </para>
/// <para>
/// <b>为什么必须是 <c>HubException</c>。</b> 一个 hub 方法抛出普通异常时,它的消息只有在
/// <c>EnableDetailedErrors</c> 打开时才送达客户端,而那被设成 <c>IsDevelopment()</c>。
/// 生产环境下 SignalR 会换成一句通用文案 —— 于是任何基于消息的客户端映射都只在开发机上
/// 工作。<c>HubException</c> 的消息**无论如何都会送达**,这正是这个类型存在的意义。
/// </para>
/// </summary>
public abstract class DomainException : Exception
{
    private static readonly Regex KebabCase = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <summary>以码和消息构造。</summary>
    /// <param name="code">稳定的 kebab-case 错误码。</param>
    /// <param name="message">人类散文,给日志看。</param>
    /// <exception cref="ArgumentException">码为空或不是 kebab-case。</exception>
    protected DomainException(string code, string message) : base(message)
    {
        if (string.IsNullOrWhiteSpace(code) || !KebabCase.IsMatch(code))
        {
            throw new ArgumentException($"Error code must be kebab-case; got '{code}'.", nameof(code));
        }
        Code = code;
    }

    /// <summary>以码、消息和内部异常构造。</summary>
    /// <param name="code">稳定的 kebab-case 错误码。</param>
    /// <param name="message">人类散文,给日志看。</param>
    /// <param name="innerException">内部异常。</param>
    protected DomainException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(code) || !KebabCase.IsMatch(code))
        {
            throw new ArgumentException($"Error code must be kebab-case; got '{code}'.", nameof(code));
        }
        Code = code;
    }

    /// <summary>这个错误的稳定身份。客户端按它选文案。</summary>
    public string Code { get; }
}
