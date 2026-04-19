namespace Gomoku.Application.Common.Exceptions;

/// <summary>指定 RoomId 在数据库中不存在。全局中间件映射为 HTTP 404。</summary>
public sealed class RoomNotFoundException : Exception
{
    /// <inheritdoc />
    public RoomNotFoundException(string message) : base(message) { }
}
