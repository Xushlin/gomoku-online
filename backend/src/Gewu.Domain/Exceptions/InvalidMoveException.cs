namespace Gomoku.Domain.Exceptions;

/// <summary>
/// 领域级非法落子异常。用于表达违反 Gomoku 不变量的行为,例如:
/// 位置越出 [0..14] 范围、落子到已有棋子的格子、以 <c>Stone.Empty</c> 构造落子等。
/// 仅用于保护 Domain 不变量;调用方(Application / AI / SignalR Hub)应先校验合法性,
/// 不要把异常当作常规流程控制手段。
/// </summary>
public sealed class InvalidMoveException : Exception
{
    /// <summary>以给定消息构造异常。</summary>
    public InvalidMoveException(string message) : base(message)
    {
    }

    /// <summary>以给定消息与内部异常构造异常。</summary>
    public InvalidMoveException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
