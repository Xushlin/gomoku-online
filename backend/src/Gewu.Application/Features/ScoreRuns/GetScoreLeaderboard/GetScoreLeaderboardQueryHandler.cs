using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Mapping;
using MediatR;

namespace Gewu.Application.Features.ScoreRuns.GetScoreLeaderboard;

/// <summary>
/// 拉分数榜:窗口 → 起始时刻(纯函数)→ 仓储分页 → 用户名批量补齐 → 映射。
/// `Rank` 按**全局**公式 `(Page - 1) * PageSize + i + 1` 计算,不随分页重置。
/// </summary>
public sealed class GetScoreLeaderboardQueryHandler
    : IRequestHandler<GetScoreLeaderboardQuery, PagedResult<ScoreLeaderboardEntryDto>>
{
    private readonly IScoreRunRepository _runs;
    private readonly IUserRepository _users;
    private readonly IDateTimeProvider _clock;

    /// <inheritdoc />
    public GetScoreLeaderboardQueryHandler(
        IScoreRunRepository runs, IUserRepository users, IDateTimeProvider clock)
    {
        _runs = runs;
        _users = users;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ScoreLeaderboardEntryDto>> Handle(
        GetScoreLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var since = ScoreWindows.StartOf(request.Window, _clock.UtcNow);

        var (entries, total) = await _runs.GetLeaderboardPagedAsync(
            request.GameKey, since, request.Page, request.PageSize, cancellationToken);

        var usernames = await _users.LookupUsernamesAsync(
            entries.Select(e => e.UserId.Value), cancellationToken);

        var rankOffset = (request.Page - 1) * request.PageSize;

        var items = entries
            .Select((e, i) => new ScoreLeaderboardEntryDto(
                Rank: rankOffset + i + 1,
                UserId: e.UserId.Value,
                // run 存在而用户行不存在是不可能的(外键 + 级联删除),空串只是不让映射抛。
                Username: usernames.TryGetValue(e.UserId.Value, out var name) ? name : string.Empty,
                Score: e.Score,
                Lines: e.Lines,
                Level: e.Level,
                FinishedAt: e.FinishedAt))
            .ToList()
            .AsReadOnly();

        return new PagedResult<ScoreLeaderboardEntryDto>(
            items, total, request.Page, request.PageSize);
    }
}
