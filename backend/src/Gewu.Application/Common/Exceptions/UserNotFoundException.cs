using Gewu.Domain.Exceptions;

namespace Gewu.Application.Common.Exceptions;

/// <summary>目标用户不存在(按 Id / Email / Username)。全局中间件映射为 HTTP 404。</summary>
public sealed class UserNotFoundException : DomainException
{
    /// <inheritdoc />
    public UserNotFoundException(string message) : base("user-not-found", message)
    {
    }
}
