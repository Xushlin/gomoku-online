using Gewu.Domain.Exceptions;

namespace Gewu.Application.Common.Exceptions;

/// <summary>注册时目标用户名已被占用(大小写不敏感)。全局中间件映射为 HTTP 409。</summary>
public sealed class UsernameAlreadyExistsException : DomainException
{
    /// <inheritdoc />
    public UsernameAlreadyExistsException(string message) : base("username-already-exists", message)
    {
    }
}
