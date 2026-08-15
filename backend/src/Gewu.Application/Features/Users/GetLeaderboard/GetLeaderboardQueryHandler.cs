using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Mapping;
using MediatR;

namespace Gewu.Application.Features.Users.GetLeaderboard;

/// <summary>
/// 分页拉取某棋种的排行榜。调仓储分页 API → 取 `(entries, total)` → 用户名经
/// <c>LookupUsernamesAsync</c> 另取 → 映射为 <see cref="LeaderboardEntryDto"/>;
/// `Rank` 按**全局**公式 `(Page - 1) * PageSize + i + 1` 计算(i 是本页 0-based 下标),
/// 使 page=2 pageSize=20 的第一个 entry 的 Rank == 21。
/// 仓储 MUST 已按 `Rating DESC, Wins DESC, GamesPlayed ASC` 排序。
/// <para>
/// 仓储返回的是 <c>UserGameStats</c> 而不是 <c>User</c> —— 榜要的是"某人在某棋种上的分"。
/// 用户名单独查:handler 里已经有一个现成的批量 lookup。
/// </para>
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
        var (entries, total) = await _users.GetLeaderboardPagedAsync(
            request.GameKey, request.Page, request.PageSize, cancellationToken);

        var usernames = await _users.LookupUsernamesAsync(
            entries.Select(e => e.UserId.Value), cancellationToken);

        var rankOffset = (request.Page - 1) * request.PageSize;

        var items = entries
            .Select((e, i) => new LeaderboardEntryDto(
                Rank: rankOffset + i + 1,
                UserId: e.UserId.Value,
                // 战绩行存在而用户行不存在是不可能的(外键 + 级联删除),空串只是不让映射抛。
                Username: usernames.TryGetValue(e.UserId.Value, out var name) ? name : string.Empty,
                Rating: e.Rating,
                GamesPlayed: e.GamesPlayed,
                Wins: e.Wins,
                Losses: e.Losses,
                Draws: e.Draws))
            .ToList()
            .AsReadOnly();

        return new PagedResult<LeaderboardEntryDto>(items, total, request.Page, request.PageSize);
    }
}
