using System.IdentityModel.Tokens.Jwt;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Features.Puzzles.CheckPuzzlePartial;
using Gewu.Application.Features.Puzzles.GetPuzzleLevel;
using Gewu.Application.Features.Puzzles.GetPuzzleLevels;
using Gewu.Application.Features.Puzzles.GetPuzzleProgress;
using Gewu.Application.Features.Puzzles.StartPuzzleAttempt;
using Gewu.Application.Features.Puzzles.SubmitPuzzleAttempt;
using Gewu.Application.Features.Puzzles.UsePuzzleHint;
using Gewu.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gewu.Api.Controllers;

/// <summary>部分校验请求体。</summary>
/// <param name="PartialJson">这一部分的提交内容。</param>
public sealed record CheckPuzzlePartialRequest(string PartialJson);

/// <summary>
/// 提示请求体。可选 —— 不带请求体时规则退化到默认揭示,而不是 400,
/// 这样一个没更新的客户端仍然拿得到提示。
/// </summary>
/// <param name="StateJson">客户端上报的盘面状态(不透明)。</param>
public sealed record UsePuzzleHintRequest(string? StateJson);

/// <summary>
/// 提交请求体。
/// <para>
/// 刻意**只有**答案一个字段:用时、错误数、提示数都是服务端事实,客户端上报的自评数值
/// 在这里没有落点,也就无法影响计分。
/// </para>
/// </summary>
/// <param name="SubmissionJson">完整答案。</param>
public sealed record SubmitPuzzleAttemptRequest(string SubmissionJson);

/// <summary>
/// 单人关卡的 REST 面。全部 <c>[Authorize]</c>,**没有任何 SignalR** ——
/// 关卡类游戏走纯 REST,玩家开一局成语纵横不应该建立 hub 连接。
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class PuzzlesController : ControllerBase
{
    private readonly ISender _mediator;

    /// <inheritdoc />
    public PuzzlesController(ISender mediator) => _mediator = mediator;

    /// <summary>取某游戏的关卡列表,含调用者的最好成绩与解锁状态。</summary>
    /// <param name="gameKey">游戏键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpGet("games/{gameKey}/levels")]
    public async Task<ActionResult<IReadOnlyList<PuzzleLevelSummaryDto>>> GetLevels(
        string gameKey, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new GetPuzzleLevelsQuery(CurrentUserId(), gameKey), cancellationToken));

    /// <summary>取单个关卡的布局。响应不含答案。</summary>
    /// <param name="gameKey">游戏键。</param>
    /// <param name="levelIndex">关卡序号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpGet("games/{gameKey}/levels/{levelIndex:int}")]
    public async Task<ActionResult<PuzzleLevelDto>> GetLevel(
        string gameKey, int levelIndex, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new GetPuzzleLevelQuery(gameKey, levelIndex), cancellationToken));

    /// <summary>取某游戏的整体进度(派生量)。</summary>
    /// <param name="gameKey">游戏键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpGet("games/{gameKey}/progress")]
    public async Task<ActionResult<PuzzleProgressDto>> GetProgress(
        string gameKey, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new GetPuzzleProgressQuery(CurrentUserId(), gameKey), cancellationToken));

    /// <summary>发起一次闯关尝试。</summary>
    /// <param name="gameKey">游戏键。</param>
    /// <param name="levelIndex">关卡序号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpPost("games/{gameKey}/levels/{levelIndex:int}/attempts")]
    public async Task<ActionResult<PuzzleAttemptStartedDto>> StartAttempt(
        string gameKey, int levelIndex, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new StartPuzzleAttemptCommand(CurrentUserId(), gameKey, levelIndex), cancellationToken));

    /// <summary>校验一份部分答案。判错时服务端记一次错。</summary>
    /// <param name="attemptId">尝试 id。</param>
    /// <param name="body">部分答案。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpPost("puzzle-attempts/{attemptId:guid}/check")]
    public async Task<ActionResult<PuzzleCheckResultDto>> Check(
        Guid attemptId, [FromBody] CheckPuzzlePartialRequest body, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new CheckPuzzlePartialCommand(CurrentUserId(), attemptId, body.PartialJson),
            cancellationToken));

    /// <summary>要一个提示。服务端揭示一个片段并计费。</summary>
    /// <param name="attemptId">尝试 id。</param>
    /// <param name="body">可选的盘面状态;缺省时退化到默认揭示。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpPost("puzzle-attempts/{attemptId:guid}/hint")]
    public async Task<ActionResult<PuzzleHintDto>> Hint(
        Guid attemptId,
        [FromBody] UsePuzzleHintRequest? body,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new UsePuzzleHintCommand(CurrentUserId(), attemptId, body?.StateJson), cancellationToken));

    /// <summary>提交完整答案。</summary>
    /// <param name="attemptId">尝试 id。</param>
    /// <param name="body">完整答案。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    [HttpPost("puzzle-attempts/{attemptId:guid}/submit")]
    public async Task<ActionResult<PuzzleSubmitResultDto>> Submit(
        Guid attemptId, [FromBody] SubmitPuzzleAttemptRequest body, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new SubmitPuzzleAttemptCommand(CurrentUserId(), attemptId, body.SubmissionJson),
            cancellationToken));

    private UserId CurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Missing sub claim.");
        return new UserId(Guid.Parse(sub));
    }
}
