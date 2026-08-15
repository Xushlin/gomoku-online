using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.Users.GetLeaderboard;

/// <summary>
/// 分页查询**某一个棋种**的排行榜。`Page` 从 1 起;`PageSize` 在 [1, 100](validator 校验)。
/// 返回 <see cref="PagedResult{T}"/>;`Rank` 是**全局名次**,计算为
/// `(Page - 1) * PageSize + i + 1`,不随分页重置。
/// <para>
/// <see cref="GameKey"/> 是**必填**的:Application 层不猜自己在被问哪个棋种。
/// "不带 gameKey 就给五子棋"这个向后兼容缺省只发生在 Api 层。
/// </para>
/// </summary>
public sealed record GetLeaderboardQuery(string GameKey, int Page, int PageSize)
    : IRequest<PagedResult<LeaderboardEntryDto>>;
