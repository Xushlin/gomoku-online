using Gewu.Domain.Exceptions;

namespace Gewu.Application.Common.Exceptions;

/// <summary>凭据正确但账号已被禁用(<c>IsActive = false</c>)。全局中间件映射为 HTTP 403。</summary>
public sealed class UserNotActiveException : DomainException
{
    /// <inheritdoc />
    public UserNotActiveException(string message) : base("user-not-active", message)
    {
    }
}
