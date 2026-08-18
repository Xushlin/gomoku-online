namespace Gewu.Application.Common.DTOs;

/// <summary>
/// 开局响应:run 的 id 与**服务端生成**的种子。
/// <para>
/// 种子必须下发 —— 客户端要用它算出同一串方块。它不是秘密(方块序列在屏幕上就能看见),
/// 与成语纵横的答案不同:那个是**扣留**式权威,这里是**重放**式权威。
/// </para>
/// </summary>
/// <param name="RunId">run id。</param>
/// <param name="GameKey">游戏键。</param>
/// <param name="Seed">方块序列的种子。</param>
/// <param name="StartedAt">开始时刻(服务端时钟)。</param>
public sealed record ScoreRunStartedDto(Guid RunId, string GameKey, int Seed, DateTime StartedAt);

/// <summary>
/// 结算响应。三个数字全部来自服务端重放 —— 客户端报的分数没有落点,也就无法影响它们。
/// </summary>
/// <param name="RunId">run id。</param>
/// <param name="Score">得分。</param>
/// <param name="Lines">累计消行数。</param>
/// <param name="Level">结束时的等级。</param>
/// <param name="Placements">被接受的放置数。</param>
/// <param name="DurationMs">服务端测得的用时(毫秒)。</param>
public sealed record ScoreRunResultDto(
    Guid RunId, int Score, int Lines, int Level, int Placements, long DurationMs);

/// <summary>
/// 分数榜单条目。仅公开展示字段。
/// <para>
/// 一个玩家在一个窗口内**只占一行**(取其最高分那一局)—— 一个被同一个人刷满十行的榜不叫榜。
/// </para>
/// </summary>
/// <param name="Rank">名次,从 1 起;不做并列处理(并列展示是前端职责)。</param>
/// <param name="UserId">玩家 id。</param>
/// <param name="Username">玩家名。</param>
/// <param name="Score">该窗口内的最高分。</param>
/// <param name="Lines">那一局的消行数。</param>
/// <param name="Level">那一局的结束等级。</param>
/// <param name="FinishedAt">那一局的结算时刻(UTC)。</param>
public sealed record ScoreLeaderboardEntryDto(
    int Rank, Guid UserId, string Username, int Score, int Lines, int Level, DateTime FinishedAt);

/// <summary>
/// 提交上来的一次放置:第几个旋转态、最左格落在哪一列。
/// <para>
/// 它是 Application 层的 DTO 而不是直接用 Domain 的 <c>TetrisPlacement</c>,因为它是**线上契约**
/// —— Api 层要能命名它来绑定请求体,而 Api 不该为此去引 Domain。handler 负责映射。
/// </para>
/// </summary>
/// <param name="Rotation">旋转态 0–3。</param>
/// <param name="Column">该旋转态最左格所在的列。</param>
public sealed record ScorePlacementDto(int Rotation, int Column);
