using Gewu.Domain.Enums;

namespace Gewu.Domain.Users;

/// <summary>
/// 一个玩家在**一个棋种**上的战绩与 ELO —— 评分数据的唯一真源。
/// <para>
/// 主键是复合的 <c>(UserId, GameKey)</c>。这些字段此前挂在 <see cref="User"/> 上,构成平台唯一的
/// 评分池 —— 它实质上就是五子棋排行榜,只是名字里没写。第二个棋种上线后那个池子必须拆开。
/// </para>
/// <para>
/// <see cref="User"/> **不保留任何镜像**(既不留"主棋种的分",也不留跨棋种聚合值)。
/// 镜像是第二份真源,漂移之后的症状是**排行榜与资料页显示不同的分**,且没有任何东西会拦住 ——
/// 与建房校验不许内联棋种白名单是同一条理由。
/// </para>
/// <para>
/// 一行只在玩家**下完**该棋种第一局时创建。"没有行"就是"没在这个棋种上下过",而排行榜的成员资格
/// 正是靠它 —— 所以为一局尚未结束的棋提前建行,会把"下过"的含义悄悄变成"点开过"。
/// </para>
/// </summary>
public sealed class UserGameStats
{
    /// <summary>新玩家在任何棋种上的起始分。</summary>
    public const int InitialRating = 1200;

    /// <summary>玩家主键(复合主键的一半)。</summary>
    public UserId UserId { get; private set; }

    /// <summary>棋种键(复合主键的另一半),与 <c>IGameRules.GameKey</c> 一致。</summary>
    public string GameKey { get; private set; } = string.Empty;

    /// <summary>该玩家在该棋种上的 ELO 积分。</summary>
    public int Rating { get; private set; }

    /// <summary>该棋种上的累计对局数。</summary>
    public int GamesPlayed { get; private set; }

    /// <summary>该棋种上的累计胜场。</summary>
    public int Wins { get; private set; }

    /// <summary>该棋种上的累计负场。</summary>
    public int Losses { get; private set; }

    /// <summary>该棋种上的累计平局数。</summary>
    public int Draws { get; private set; }

    /// <summary>
    /// 乐观并发令牌,保护**本行**的战绩写入。
    /// <para>
    /// 与 <see cref="User.RowVersion"/> 分开是有具体收益的:一个玩家一边下棋一边改密码,此前会撞
    /// 409;同一玩家两个不同棋种的对局同时结束也会互撞。现在两者写的是不同的行,各自的令牌互不干涉。
    /// </para>
    /// </summary>
    public byte[] RowVersion { get; private set; } = default!;

    // EF 物化用。
    private UserGameStats() { }

    /// <summary>
    /// 为某玩家在某棋种上开一行初始战绩:<c>Rating = 1200</c>,四个计数器归零。
    /// </summary>
    /// <param name="userId">玩家。</param>
    /// <param name="gameKey">棋种键,非空。</param>
    /// <exception cref="ArgumentException"><paramref name="gameKey"/> 为空 / 空白。</exception>
    public static UserGameStats Start(UserId userId, string gameKey)
    {
        if (string.IsNullOrWhiteSpace(gameKey))
        {
            throw new ArgumentException("Game key must be non-empty.", nameof(gameKey));
        }

        return new UserGameStats
        {
            UserId = userId,
            GameKey = gameKey,
            Rating = InitialRating,
            GamesPlayed = 0,
            Wins = 0,
            Losses = 0,
            Draws = 0,
            RowVersion = NewRowVersion(),
        };
    }

    /// <summary>
    /// 记录该棋种上的一局结果,原子完成:<c>GamesPlayed++</c>、按 <paramref name="outcome"/> 递增对应
    /// 计数器、把 <see cref="Rating"/> 设为 <paramref name="newRating"/>,并推进 <see cref="RowVersion"/>。
    /// <para>
    /// 这是 elo-rating 对本实体的唯一写入口;调用方 MUST 先用 <c>EloRating.Calculate</c> 算出新积分
    /// 再调用 —— 本方法不做 ELO 计算,也不校验 <paramref name="newRating"/> 的合理性。
    /// </para>
    /// <para>
    /// 不变量:调用后 <c>Wins + Losses + Draws == GamesPlayed</c>。它现在**对每个棋种各自成立**。
    /// </para>
    /// </summary>
    /// <param name="outcome">本方视角下的结果。</param>
    /// <param name="newRating">算好的新积分。</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outcome"/> 不是已定义值;抛出时本行状态保持不变(含 <see cref="RowVersion"/>)。
    /// </exception>
    public void RecordGameResult(GameOutcome outcome, int newRating)
    {
        switch (outcome)
        {
            case GameOutcome.Win:
                Wins++;
                break;
            case GameOutcome.Loss:
                Losses++;
                break;
            case GameOutcome.Draw:
                Draws++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown GameOutcome value.");
        }

        GamesPlayed++;
        Rating = newRating;
        RowVersion = NewRowVersion();
    }

    private static byte[] NewRowVersion() => Guid.NewGuid().ToByteArray();
}
