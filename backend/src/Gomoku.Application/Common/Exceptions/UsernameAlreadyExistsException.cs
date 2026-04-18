namespace Gomoku.Application.Common.Exceptions;

/// <summary>注册时目标用户名已被占用(大小写不敏感)。全局中间件映射为 HTTP 409。</summary>
public sealed class UsernameAlreadyExistsException : Exception
{
    /// <inheritdoc />
    public UsernameAlreadyExistsException(string message) : base(message)
    {
    }
}
