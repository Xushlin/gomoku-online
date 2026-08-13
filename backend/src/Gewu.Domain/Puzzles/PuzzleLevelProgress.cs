using Gewu.Domain.Users;

namespace Gewu.Domain.Puzzles;

/// <summary>
/// 某用户在某一关的**最好成绩**,每 <c>(UserId, PuzzleLevelId)</c> 一行。
/// <para>
/// 只升不降:重玩不会拉低已有评级 —— 这是所有关卡制游戏的既有行为,也是排行榜能稳定的前提。
/// </para>
/// <para>
/// 这里刻意**没有**"已解锁下标"和"总星数"两个字段:它们分别是
/// <c>MAX(已完成关卡 LevelIndex) + 1</c> 与 <c>SUM(BestStars)</c>,都是查询。
/// 反范式计数器会跟产生它的行不一致(失败事务、手工修数、两条写路径里的一个 bug),
/// 而"每人每关最多一行"的量级下两个聚合查询没有可观成本。
/// </para>
/// </summary>
public sealed class PuzzleLevelProgress
{
    /// <summary>用户。与 <see cref="PuzzleLevelId"/> 构成复合主键。</summary>
    public UserId UserId { get; private set; }

    /// <summary>关卡。</summary>
    public int PuzzleLevelId { get; private set; }

    /// <summary>历史最好星级(1–3)。</summary>
    public int BestStars { get; private set; }

    /// <summary>取得 <see cref="BestStars"/> 时的用时(毫秒)。</summary>
    public long BestDurationMs { get; private set; }

    /// <summary>累计通关次数 —— 统计量,不是成绩,每次完成都递增。</summary>
    public int AttemptCount { get; private set; }

    // EF 物化用。
    private PuzzleLevelProgress() { }

    /// <summary>为某用户在某关创建首条最好成绩。</summary>
    /// <param name="userId">用户。</param>
    /// <param name="puzzleLevelId">关卡。</param>
    /// <param name="stars">首次通关星级。</param>
    /// <param name="durationMs">首次通关用时(毫秒)。</param>
    public static PuzzleLevelProgress First(UserId userId, int puzzleLevelId, int stars, long durationMs)
        => new()
        {
            UserId = userId,
            PuzzleLevelId = puzzleLevelId,
            BestStars = stars,
            BestDurationMs = durationMs,
            AttemptCount = 1,
        };

    /// <summary>
    /// 记录又一次通关。<see cref="AttemptCount"/> 无条件 +1;
    /// <see cref="BestStars"/> / <see cref="BestDurationMs"/> **仅在成绩更好时**更新
    /// —— 星级更高,或星级相同而用时更短。
    /// </summary>
    /// <param name="stars">本次星级。</param>
    /// <param name="durationMs">本次用时(毫秒)。</param>
    /// <returns>最好成绩是否被本次刷新。</returns>
    public bool RecordCompletion(int stars, long durationMs)
    {
        AttemptCount++;

        var better = stars > BestStars
            || (stars == BestStars && durationMs < BestDurationMs);

        if (!better)
        {
            return false;
        }

        BestStars = stars;
        BestDurationMs = durationMs;
        return true;
    }
}
