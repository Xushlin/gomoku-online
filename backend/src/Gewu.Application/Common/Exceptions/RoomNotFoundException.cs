using Gewu.Domain.Exceptions;

namespace Gewu.Application.Common.Exceptions;

/// <summary>指定 RoomId 在数据库中不存在。全局中间件映射为 HTTP 404。</summary>
public sealed class RoomNotFoundException : DomainException
{
    /// <inheritdoc />
    public RoomNotFoundException(string message) : base("room-not-found", message) { }
}
