namespace Gewu.Domain.Exceptions;

/// <summary>
/// 对一个已结束的 run 再次提交时抛出。
/// <para>
/// 错误码与闯关的 <see cref="AttemptAlreadyFinishedException"/> **刻意不同**,尽管两者防的是
/// 同一件事。客户端按码显示文案,而"这一局已经结算过了"与"这次闯关已经结束了"是两句不同的话;
/// 共用一个码会让其中一句只能说得含糊。
/// </para>
/// </summary>
public sealed class ScoreRunAlreadyFinishedException : DomainException
{
    /// <inheritdoc />
    public ScoreRunAlreadyFinishedException(string message)
        : base("score-run-already-finished", message) { }
}
