namespace Gomoku.Application.Common.Exceptions;

/// <summary>
/// 提交的 refresh token 不合法:不存在 / 已过期 / 已吊销。全局中间件映射为 HTTP 401。
/// 消息统一为 <c>"Refresh token is invalid or expired."</c>,不细分具体原因。
/// </summary>
public sealed class InvalidRefreshTokenException : Exception
{
    /// <inheritdoc />
    public InvalidRefreshTokenException()
        : base("Refresh token is invalid or expired.")
    {
    }
}
