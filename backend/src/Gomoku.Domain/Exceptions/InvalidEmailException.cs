namespace Gomoku.Domain.Exceptions;

/// <summary>
/// 领域级非法邮箱异常。用于表达 <c>Email</c> 值对象构造时发现的格式问题:
/// 空 / 超长(&gt; 254 字符)/ 不符合 RFC 5321/5322 基本语法。消息包含违反的具体规则,
/// 便于日志定位与前端展示。
/// </summary>
public sealed class InvalidEmailException : Exception
{
    /// <summary>以给定消息构造异常。</summary>
    public InvalidEmailException(string message) : base(message)
    {
    }

    /// <summary>以给定消息与内部异常构造异常。</summary>
    public InvalidEmailException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
