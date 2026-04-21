namespace Gomoku.Application.Common.Exceptions;

/// <summary>
/// 对 Playing / Waiting 状态房间请求 `/replay` 端点时抛出。Api 层映射 HTTP 409,
/// 与 `RoomNotInPlayException` / `RoomNotWaitingException` 等语义一致(状态不匹配)。
/// </summary>
public sealed class GameNotFinishedException : Exception
{
    /// <inheritdoc />
    public GameNotFinishedException(string message) : base(message) { }
}
