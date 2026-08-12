using Gomoku.Domain.Users;

namespace Gomoku.Domain.EloRating;

/// <summary>
/// 标准 Harvard-Sargon(HS)ELO 积分计算。纯函数 —— 相同入参必产相同出参,
/// 不读取时钟、随机、IO 或任何外部状态,便于表驱动测试。
/// <para>
/// 公式(A 方视角):<br/>
/// <c>expectedA = 1 / (1 + 10^((ratingB - ratingA) / 400))</c><br/>
/// <c>scoreA = Win → 1.0, Draw → 0.5, Loss → 0.0</c><br/>
/// <c>newRatingA = ratingA + round(kA * (scoreA - expectedA))</c><br/>
/// <c>newRatingB = ratingB + round(kB * ((1 - scoreA) - (1 - expectedA)))</c>
/// </para>
/// <para>
/// K 因子按**己方** <c>gamesPlayed</c> 分段:<c>&lt;30 → 40</c>、<c>&lt;100 → 20</c>、<c>≥100 → 10</c>。
/// 这意味着两方**非对称**(新手输给老手,新手掉分比老手赚的多),是业界 HS ELO 通用做法。
/// </para>
/// <para>
/// 舍入采用 <see cref="MidpointRounding.AwayFromZero"/>,避免 banker's rounding 让 .5 偏向偶数。
/// </para>
/// </summary>
public static class EloRating
{
    /// <summary>
    /// 计算一局对局后双方的新积分。
    /// </summary>
    /// <param name="ratingA">A 方当前积分。</param>
    /// <param name="gamesA">A 方已下对局数 —— 用于决定 A 方 K 因子。</param>
    /// <param name="ratingB">B 方当前积分。</param>
    /// <param name="gamesB">B 方已下对局数 —— 用于决定 B 方 K 因子。</param>
    /// <param name="outcomeA">A 方视角的结果(Win / Loss / Draw)。</param>
    /// <returns>新积分 tuple:<c>(NewRatingA, NewRatingB)</c>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcomeA"/> 不是定义值。</exception>
    public static (int NewRatingA, int NewRatingB) Calculate(
        int ratingA,
        int gamesA,
        int ratingB,
        int gamesB,
        GameOutcome outcomeA)
    {
        var scoreA = outcomeA switch
        {
            GameOutcome.Win => 1.0,
            GameOutcome.Draw => 0.5,
            GameOutcome.Loss => 0.0,
            _ => throw new ArgumentOutOfRangeException(nameof(outcomeA), outcomeA, "Unknown GameOutcome value."),
        };

        var expectedA = 1.0 / (1 + Math.Pow(10, (ratingB - ratingA) / 400.0));
        var kA = KFactor(gamesA);
        var kB = KFactor(gamesB);

        var deltaA = (int)Math.Round(kA * (scoreA - expectedA), MidpointRounding.AwayFromZero);
        var deltaB = (int)Math.Round(kB * ((1 - scoreA) - (1 - expectedA)), MidpointRounding.AwayFromZero);

        return (ratingA + deltaA, ratingB + deltaB);
    }

    private static int KFactor(int gamesPlayed) => gamesPlayed switch
    {
        < 30 => 40,
        < 100 => 20,
        _ => 10,
    };
}
