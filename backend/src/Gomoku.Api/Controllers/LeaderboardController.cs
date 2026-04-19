using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Features.Users.GetLeaderboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gomoku.Api.Controllers;

/// <summary>
/// 排行榜只读端点。返回按 <c>Rating DESC, Wins DESC, GamesPlayed ASC</c> 排好的前 100 位用户。
/// 不接受 query 参数;分页 / 搜索 / 过滤留给后续变更。
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

    /// <summary>返回前 100 位的 <see cref="LeaderboardEntryDto"/>(可能为空)。</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryDto>>> Get(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetLeaderboardQuery(), cancellationToken);
        return Ok(result);
    }
}
