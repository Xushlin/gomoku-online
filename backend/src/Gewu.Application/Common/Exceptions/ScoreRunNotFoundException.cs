using Gewu.Domain.Exceptions;

namespace Gewu.Application.Common.Exceptions;

/// <summary>
/// run 不存在,或游戏键不是一个计分类游戏时抛出 —— 统一映射为 404。
/// <para>
/// **run 的所有权检查也走这里**,而不是 403:返回 404 就不会向调用方泄漏
/// "这个 id 确实存在,只是不属于你"。与 <see cref="PuzzleNotFoundException"/> 同规。
/// </para>
/// </summary>
public sealed class ScoreRunNotFoundException : DomainException
{
    /// <inheritdoc />
    public ScoreRunNotFoundException(string message) : base("score-run-not-found", message) { }
}
