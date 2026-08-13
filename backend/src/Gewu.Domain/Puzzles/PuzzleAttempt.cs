using Gewu.Domain.Exceptions;
using Gewu.Domain.Users;

namespace Gewu.Domain.Puzzles;

/// <summary>
/// 一次闯关尝试 —— 本上下文的**权威单位**。
/// <para>
/// 提示数、错误数、起止时间全部记在这里,且只能经本类的领域方法修改;三个方法都拒绝一个
/// 已结束的尝试。客户端因此无法在提交后继续要提示,也无法重复提交刷一个更好的分。
/// </para>
/// <para>
/// 用时取 <see cref="FinishedAt"/> − <see cref="StartedAt"/>,两端都是服务端时钟
/// (handler 经 <c>IDateTimeProvider</c> 提供)—— 客户端上报的任何时间都不参与。
/// </para>
/// </summary>
public sealed class PuzzleAttempt
{
    private const int MinStars = 1;
    private const int MaxStars = 3;

    /// <summary>主键。</summary>
    public Guid Id { get; private set; }

    /// <summary>发起者。</summary>
    public UserId UserId { get; private set; }

    /// <summary>所属关卡。</summary>
    public int PuzzleLevelId { get; private set; }

    /// <summary>开始时间(UTC,服务端时钟)。</summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>结束时间(UTC);进行中为 <c>null</c>。</summary>
    public DateTime? FinishedAt { get; private set; }

    /// <summary>已用提示数 —— 由服务端发放时递增。</summary>
    public int HintsUsed { get; private set; }

    /// <summary>错误数 —— 由服务端在部分校验判错时递增,不采信客户端上报。</summary>
    public int Mistakes { get; private set; }

    /// <summary>通关星级(1–3);未通关为 <c>null</c>。</summary>
    public int? Stars { get; private set; }

    /// <summary>是否已通关。</summary>
    public bool IsCompleted => Stars is not null;

    /// <summary>
    /// 乐观并发令牌。SQLite 无原生 rowversion,由 Domain 在每次状态变更后自行刷新
    /// —— 与 <c>Game</c> / <c>User</c> 同一纪律,防止并发 check / hint 丢更新。
    /// </summary>
    public byte[] RowVersion { get; private set; } = Guid.NewGuid().ToByteArray();

    // EF 物化用。
    private PuzzleAttempt() { }

    /// <summary>发起一次尝试。</summary>
    /// <param name="id">主键。</param>
    /// <param name="userId">发起者。</param>
    /// <param name="puzzleLevelId">关卡。</param>
    /// <param name="startedAt">开始时间(服务端时钟)。</param>
    public static PuzzleAttempt Start(Guid id, UserId userId, int puzzleLevelId, DateTime startedAt)
        => new()
        {
            Id = id,
            UserId = userId,
            PuzzleLevelId = puzzleLevelId,
            StartedAt = startedAt,
            FinishedAt = null,
            HintsUsed = 0,
            Mistakes = 0,
            Stars = null,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };

    /// <summary>记一次错误(部分校验判错,或提交时答案不符)。</summary>
    /// <exception cref="AttemptAlreadyFinishedException">尝试已结束。</exception>
    public void RecordMistake()
    {
        EnsureUnfinished(nameof(RecordMistake));
        Mistakes++;
        TouchRowVersion();
    }

    /// <summary>记一次提示消耗。</summary>
    /// <exception cref="AttemptAlreadyFinishedException">尝试已结束。</exception>
    public void RecordHint()
    {
        EnsureUnfinished(nameof(RecordHint));
        HintsUsed++;
        TouchRowVersion();
    }

    /// <summary>标记通关。</summary>
    /// <param name="stars">星级,必须在 [1..3]。</param>
    /// <param name="finishedAt">结束时间(服务端时钟)。</param>
    /// <exception cref="AttemptAlreadyFinishedException">尝试已结束 —— 重复提交被拒。</exception>
    /// <exception cref="InvalidStarRatingException"><paramref name="stars"/> 越界。</exception>
    public void Complete(int stars, DateTime finishedAt)
    {
        EnsureUnfinished(nameof(Complete));

        if (stars < MinStars || stars > MaxStars)
        {
            throw new InvalidStarRatingException(
                $"Star rating {stars} is out of range [{MinStars}..{MaxStars}].");
        }

        Stars = stars;
        FinishedAt = finishedAt;
        TouchRowVersion();
    }

    /// <summary>服务端测得的用时;未结束时为 <c>null</c>。</summary>
    public TimeSpan? Duration => FinishedAt is null ? null : FinishedAt.Value - StartedAt;

    private void EnsureUnfinished(string operation)
    {
        if (FinishedAt is not null)
        {
            throw new AttemptAlreadyFinishedException(
                $"Cannot {operation} on attempt {Id} — it finished at {FinishedAt:o}.");
        }
    }

    private void TouchRowVersion() => RowVersion = Guid.NewGuid().ToByteArray();
}
