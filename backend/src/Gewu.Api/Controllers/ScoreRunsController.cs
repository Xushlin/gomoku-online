using System.IdentityModel.Tokens.Jwt;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Features.ScoreRuns.GetScoreLeaderboard;
using Gewu.Application.Features.ScoreRuns.StartScoreRun;
using Gewu.Application.Features.ScoreRuns.SubmitScoreRun;
using Gewu.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gewu.Api.Controllers;

/// <summary>
/// 开局请求体。
/// <para>
/// <c>gameKey</c> **必填**,没有服务端缺省 —— <c>require-room-game-key</c> 的结论:
/// 一个填在服务端的缺省键,是一个藏在客户端读者找不到的地方的硬编码游戏。
/// </para>
/// </summary>
/// <param name="GameKey">游戏键。</param>
public sealed record StartScoreRunRequest(string GameKey);

/// <summary>
/// 结算请求体。
/// <para>
/// 刻意**只有**放置序列一个字段:分数、消行、等级、用时全是服务端事实,客户端上报的自评数值
/// 在这里没有落点,也就无法影响计分。
/// </para>
/// </summary>
/// <param name="Placements">按顺序的放置。</param>
public sealed record SubmitScoreRunRequest(IReadOnlyList<ScorePlacementDto>? Placements);

/// <summary>
/// 计分类游戏的 REST 面。全部 <c>[Authorize]</c>,**没有任何 SignalR** ——
/// 计分类是单人的,一局结束时提交一次,玩家开一局俄罗斯方块不该建立 hub 连接。
/// </summary>
[ApiController]
[Authorize]
[Route("api/score-runs")]
public sealed class ScoreRunsController : ControllerBase
{
    private readonly ISender _mediator;

    /// <inheritdoc />
    public ScoreRunsController(ISender mediator) => _mediator = mediator;

    /// <summary>开一局。返回 run id 与服务端生成的方块序列种子。</summary>
    /// <param name="body">开局请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpPost]
    public async Task<ActionResult<ScoreRunStartedDto>> Start(
        [FromBody] StartScoreRunRequest body, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new StartScoreRunCommand(CurrentUserId(), body.GameKey), cancellationToken));

    /// <summary>提交一局的放置序列。服务端重放并写入分数。</summary>
    /// <param name="runId">run id。</param>
    /// <param name="body">放置序列。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpPost("{runId:guid}/submit")]
    public async Task<ActionResult<ScoreRunResultDto>> Submit(
        Guid runId, [FromBody] SubmitScoreRunRequest body, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new SubmitScoreRunCommand(CurrentUserId(), runId, body.Placements ?? []),
            cancellationToken));

    /// <summary>分数榜。窗口 <c>week</c>(自然周)/ <c>month</c> / <c>all</c>。</summary>
    /// <param name="gameKey">游戏键。</param>
    /// <param name="window">时间窗口,缺省 <c>week</c>。</param>
    /// <param name="page">页码,从 1 起。</param>
    /// <param name="pageSize">每页条数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpGet("leaderboard")]
    public async Task<ActionResult<PagedResult<ScoreLeaderboardEntryDto>>> Leaderboard(
        [FromQuery] string gameKey,
        [FromQuery] ScoreWindow window = ScoreWindow.Week,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(await _mediator.Send(
            new GetScoreLeaderboardQuery(gameKey, window, page, pageSize), cancellationToken));

    private UserId CurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Missing sub claim.");
        return new UserId(Guid.Parse(sub));
    }
}
