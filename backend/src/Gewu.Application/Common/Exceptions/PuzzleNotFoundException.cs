using Gewu.Domain.Exceptions;

namespace Gewu.Application.Common.Exceptions;

/// <summary>
/// 关卡、尝试,或游戏键对应的规则不存在时抛出 —— 统一映射为 404。
/// <para>
/// **尝试的所有权检查也走这里**,而不是 403:返回 404 就不会向调用方泄漏
/// "这个 id 确实存在,只是不属于你"。
/// </para>
/// </summary>
public sealed class PuzzleNotFoundException : DomainException
{
    /// <inheritdoc />
    public PuzzleNotFoundException(string message) : base("puzzle-not-found", message) { }
}
