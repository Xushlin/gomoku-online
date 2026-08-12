using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Features.Users.GetLeaderboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gomoku.Api.Controllers;

/// <summary>
/// 排行榜只读端点。返回按 <c>Rating DESC, Wins DESC, GamesPlayed ASC</c> 排好的真人用户,
/// 按 <c>page</c> / <c>pageSize</c> 分页(默认 page=1, pageSize=20;pageSize ≤ 100)。
/// bot 账号跟随 ELO 正常更新但 MUST NOT 进榜单。
/// </summary>
[ApiController]
[Route("api/leaderboard")]
[Authorize]
public sealed class LeaderboardController : ControllerBase
{
    private readonly ISender _mediator;

    /// <inheritdoc />
    public LeaderboardController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 返回 <see cref="PagedResult{T}"/> 包装的排行榜;Rank 是**全局名次**
    /// (page=2 / pageSize=20 的第一个 entry Rank == 21),非页内序号。
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<LeaderboardEntryDto>>> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetLeaderboardQuery(page, pageSize), cancellationToken);
        return Ok(result);
    }
}
