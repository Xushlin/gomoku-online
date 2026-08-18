namespace Gewu.Application.Features.ScoreRuns.GetScoreLeaderboard;

/// <summary>分数榜的时间窗口。</summary>
public enum ScoreWindow
{
    /// <summary>本自然周 —— 周一 00:00 UTC 起。</summary>
    Week = 0,

    /// <summary>本自然月 —— 1 日 00:00 UTC 起。</summary>
    Month = 1,

    /// <summary>不按时间过滤。</summary>
    All = 2,
}

/// <summary>
/// 窗口 → 起始时刻。纯函数,所以自然周这条规则可以不碰数据库就测。
/// </summary>
public static class ScoreWindows
{
    /// <summary>
    /// 窗口的**起始时刻**(含);<see cref="ScoreWindow.All"/> 返回 <c>null</c> 表示不过滤。
    /// <para>
    /// 周是**自然周**而不是滚动 7 天:自然周有一个所有人共享的截止时刻,于是"本周还剩两天"
    /// 是一句对每个人都成立的话;滚动窗口对每个人在每一刻都不同,而一个昨天还在榜上的成绩会
    /// 今天悄无声息地掉下去 —— 玩家看到的是名次莫名其妙地变了。代价是周一清零显得突然,
    /// 接受它,因为**可预期的残忍好过不可解释的漂移**。
    /// </para>
    /// <para>
    /// 边界取 UTC 而不是服务器本地时区:后者会让同一个榜在不同部署下切在不同时刻,
    /// 而那是一个没人会想到去查的差异。
    /// </para>
    /// </summary>
    /// <param name="window">窗口。</param>
    /// <param name="now">当前时刻(UTC,服务端时钟)。</param>
    public static DateTime? StartOf(ScoreWindow window, DateTime now) => window switch
    {
        // DayOfWeek 的周日是 0,而自然周从周一起 —— +6 再取模把周日挪到第 6 天。
        // 直接用 (int)DayOfWeek - 1 会让周日回退到"上周一的前一天",把周日整天甩出本周。
        ScoreWindow.Week => DateTime.SpecifyKind(
            now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7)), DateTimeKind.Utc),
        ScoreWindow.Month => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
        ScoreWindow.All => null,
        // 未定义的枚举值**必须炸**,不能落进一个"当成 all"的兜底分支。
        // 兜底会让一个打错的窗口静静返回全部历史 —— 那是最不该发生的那种"成功",
        // 而且它把正确性押在"上游总记得校验"上。这里改成大声失败,校验就只是为了让
        // 失败长成一个带字段名的 400,而不是唯一的防线。
        _ => throw new ArgumentOutOfRangeException(
            nameof(window), window, "Unknown score leaderboard window."),
    };
}
