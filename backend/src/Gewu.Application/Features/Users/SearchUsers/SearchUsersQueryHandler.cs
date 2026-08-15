using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Mapping;
using Gewu.Domain.Games.Abstractions;
using MediatR;

namespace Gewu.Application.Features.Users.SearchUsers;

/// <summary>
/// 调仓储分页 API → 批量取这一页用户的五子棋战绩 → 映射为 <see cref="UserPublicProfileDto"/> →
/// 包 <see cref="PagedResult{T}"/>。Bot 过滤与 prefix 大小写处理都在仓储层完成;handler 只做 shape 转换。
/// <para>
/// **棋种钉在五子棋,不加参数。** 理由不是省事:找人卡片是**五子棋大厅**的一个组件
/// (`pages/lobby/cards/find-player`),让它按棋种参数化等于开始泛化大厅 —— 那是 roadmap 上单独
/// 的一步,会动到 `/home` 在五份 web spec 里的规范地位。一个变更做一件事。
/// </para>
/// </summary>
public sealed class SearchUsersQueryHandler
    : IRequestHandler<SearchUsersQuery, PagedResult<UserPublicProfileDto>>
{
    private readonly IUserRepository _users;

    /// <inheritdoc />
    public SearchUsersQueryHandler(IUserRepository users)
    {
        _users = users;
    }

    /// <inheritdoc />
    public async Task<PagedResult<UserPublicProfileDto>> Handle(
        SearchUsersQuery request, CancellationToken cancellationToken)
    {
        var (users, total) = await _users.SearchByUsernamePagedAsync(
            request.Search, request.Page, request.PageSize, cancellationToken);

        // 一次批量查而不是逐人一次 —— 一页 20 人就是 20 次往返的差别。
        var stats = await _users.FindGameStatsForAsync(
            users.Select(u => u.Id), GameKeys.Gomoku, cancellationToken);

        var items = users
            .Select(u => u.ToPublicProfileDto(
                stats.TryGetValue(u.Id.Value, out var s) ? s : null))
            .ToList()
            .AsReadOnly();

        return new PagedResult<UserPublicProfileDto>(items, total, request.Page, request.PageSize);
    }
}
