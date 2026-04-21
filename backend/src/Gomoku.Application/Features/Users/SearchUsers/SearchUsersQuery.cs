using Gomoku.Application.Common.DTOs;
using MediatR;

namespace Gomoku.Application.Features.Users.SearchUsers;

/// <summary>
/// 按用户名前缀(大小写不敏感)分页搜索真人。<see cref="Search"/> 允许 <c>null</c> 或空串
/// (= 浏览所有真人,按 Username ASC)。bot 账号永远不在搜索结果。
/// </summary>
public sealed record SearchUsersQuery(
    string? Search,
    int Page,
    int PageSize) : IRequest<PagedResult<UserPublicProfileDto>>;
