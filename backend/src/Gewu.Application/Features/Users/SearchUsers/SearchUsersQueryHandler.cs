using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using MediatR;

namespace Gomoku.Application.Features.Users.SearchUsers;

/// <summary>
/// 调仓储分页 API → 映射为 <see cref="UserPublicProfileDto"/> → 包 <see cref="PagedResult{T}"/>。
/// Bot 过滤与 prefix 大小写处理都在仓储层完成;handler 只做 shape 转换。
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

        var items = users
            .Select(u => new UserPublicProfileDto(
                Id: u.Id.Value,
                Username: u.Username.Value,
                Rating: u.Rating,
                GamesPlayed: u.GamesPlayed,
                Wins: u.Wins,
                Losses: u.Losses,
                Draws: u.Draws,
                CreatedAt: u.CreatedAt))
            .ToList()
            .AsReadOnly();

        return new PagedResult<UserPublicProfileDto>(items, total, request.Page, request.PageSize);
    }
}
