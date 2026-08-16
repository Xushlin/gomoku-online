namespace Gewu.Domain.Exceptions;

/// <summary>对一个已结束的尝试做出修改时抛出。</summary>
public sealed class AttemptAlreadyFinishedException : DomainException
{
    /// <inheritdoc />
    public AttemptAlreadyFinishedException(string message) : base("attempt-already-finished", message) { }
}

/// <summary>星级不在 [1..3] 时抛出 —— 防止某个游戏的 <c>Score</c> 实现返回越界值。</summary>
public sealed class InvalidStarRatingException : DomainException
{
    /// <inheritdoc />
    public InvalidStarRatingException(string message) : base("invalid-star-rating", message) { }
}
