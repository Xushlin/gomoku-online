using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using MediatR;

namespace Gomoku.Application.Features.Users.GetLeaderboard;

/// <summary>
/// 分页拉取排行榜。调仓储分页 API → 取 `(users, total)` → 映射为 <see cref="LeaderboardEntryDto"/>;
/// `Rank` 按**全局**公式 `(Page - 1) * PageSize + i + 1` 计算(i 是本页 0-based 下标),
/// 使 page=2 pageSize=20 的第一个 entry 的 Rank == 21。
/// 仓储 MUST 已按 `Rating DESC, Wins DESC, GamesPlayed ASC` 排序。
/// </summary>
public sealed class GetLeaderboardQueryHandler
    : IRequestHandler<GetLeaderboardQuery, PagedResult<LeaderboardEntryDto>>
{
    private readonly IUserRepository _users;

    /// <inheritdoc />
    public GetLeaderboardQueryHandler(IUserRepository users)
    {
        _users = users;
    }

    /// <inheritdoc />
    public async Task<PagedResult<LeaderboardEntryDto>> Handle(
        GetLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var (users, total) = await _users.GetLeaderboardPagedAsync(
            request.Page, request.PageSize, cancellationToken);

        var rankOffset = (request.Page - 1) * request.PageSize;

        var items = users
            .Select((u, i) => new LeaderboardEntryDto(
                Rank: rankOffset + i + 1,
                UserId: u.Id.Value,
                Username: u.Username.Value,
                Rating: u.Rating,
                GamesPlayed: u.GamesPlayed,
                Wins: u.Wins,
                Losses: u.Losses,
                Draws: u.Draws))
            .ToList()
            .AsReadOnly();

        return new PagedResult<LeaderboardEntryDto>(items, total, request.Page, request.PageSize);
    }
}
