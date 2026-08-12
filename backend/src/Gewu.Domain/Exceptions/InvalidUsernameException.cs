namespace Gomoku.Domain.Exceptions;

/// <summary>
/// 领域级非法用户名异常。用于表达 <c>Username</c> 值对象构造时发现的问题:
/// 长度不在 [3..20]、包含非允许字符集(字母 / 数字 / 下划线 / 中文 BMP)、
/// 全部由数字组成。消息指明违反的具体规则。
/// </summary>
public sealed class InvalidUsernameException : Exception
{
    /// <summary>以给定消息构造异常。</summary>
    public InvalidUsernameException(string message) : base(message)
    {
    }

    /// <summary>以给定消息与内部异常构造异常。</summary>
    public InvalidUsernameException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
