namespace Gomoku.Application.Common.Exceptions;

/// <summary>
/// 登录时邮箱或密码错误。消息**故意模糊**,不指出是哪个字段错,避免通过此接口枚举已注册邮箱。
/// 全局中间件映射为 HTTP 401。
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    /// <summary>默认消息:<c>"Email or password is incorrect."</c>(避免泄漏哪边错)。</summary>
    public InvalidCredentialsException()
        : base("Email or password is incorrect.")
    {
    }
}
