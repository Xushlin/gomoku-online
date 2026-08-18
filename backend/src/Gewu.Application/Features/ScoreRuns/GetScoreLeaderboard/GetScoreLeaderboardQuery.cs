using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.ScoreRuns.GetScoreLeaderboard;

/// <summary>
/// 分页查询某计分类游戏的分数榜。`Page` 从 1 起;`PageSize` 在 [1, 100]。
/// <para>
/// <see cref="GameKey"/> 是**必填**的:Application 层不猜自己在被问哪个游戏
/// —— 与 <c>GetLeaderboardQuery</c> 同规。
/// </para>
/// </summary>
/// <param name="GameKey">游戏键。</param>
/// <param name="Window">时间窗口。</param>
/// <param name="Page">页码,从 1 起。</param>
/// <param name="PageSize">每页条数。</param>
public sealed record GetScoreLeaderboardQuery(
    string GameKey, ScoreWindow Window, int Page, int PageSize)
    : IRequest<PagedResult<ScoreLeaderboardEntryDto>>;
