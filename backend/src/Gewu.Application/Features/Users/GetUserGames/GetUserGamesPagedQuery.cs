using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Users.GetUserGames;

/// <summary>
/// 分页拉取用户参与过的 Finished 对局战绩。<see cref="Page"/> 从 1 起;<see cref="PageSize"/>
/// 在 [1, 100];非法输入由 validator 抛 <see cref="Gomoku.Application.Common.Exceptions.ValidationException"/>(HTTP 400)。
/// 排序:按对局 EndedAt 降序(最近一局在前)。
/// </summary>
public sealed record GetUserGamesPagedQuery(
    UserId UserId,
    int Page,
    int PageSize) : IRequest<PagedResult<UserGameSummaryDto>>;
