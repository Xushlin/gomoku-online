using Gomoku.Application.Common.DTOs;
using MediatR;

namespace Gomoku.Application.Features.Users.GetLeaderboard;

/// <summary>
/// 分页查询排行榜。`Page` 从 1 起;`PageSize` 在 [1, 100](validator 校验)。
/// 返回 <see cref="PagedResult{T}"/>;`Rank` 是**全局名次**,计算为
/// `(Page - 1) * PageSize + i + 1`,不随分页重置。
/// </summary>
public sealed record GetLeaderboardQuery(int Page, int PageSize)
    : IRequest<PagedResult<LeaderboardEntryDto>>;
