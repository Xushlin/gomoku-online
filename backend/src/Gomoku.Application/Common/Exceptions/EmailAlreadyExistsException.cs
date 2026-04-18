namespace Gomoku.Application.Common.Exceptions;

/// <summary>注册 / 更改邮箱时发现目标邮箱已被占用。全局中间件映射为 HTTP 409。</summary>
public sealed class EmailAlreadyExistsException : Exception
{
    /// <inheritdoc />
    public EmailAlreadyExistsException(string message) : base(message)
    {
    }
}
