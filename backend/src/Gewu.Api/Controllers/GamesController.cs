using Gewu.Application.Common.DTOs;
using Gewu.Application.Features.Games.GetGameDescriptors;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gewu.Api.Controllers;

/// <summary>
/// 对战棋种目录 —— <c>IGameRulesRegistry</c> 的只读投影。
/// <para>
/// 客户端靠它知道**哪些棋种计分**(亦即哪些有排行榜)以及各自的盘面尺寸。没有它,前端就得
/// 自己维护一份"哪些棋种计分"的副本,而那种副本失配的症状是一个永远空着的榜 ——
/// 与"新棋种还没人下过"在屏幕上一模一样,也就是说失配不会被发现。
/// </para>
/// <para>
/// 只覆盖对战棋种。谜题类走 <c>PuzzlesController</c>,那是另一条线。
/// </para>
/// </summary>
[ApiController]
[Route("api/games")]
[Authorize]
public sealed class GamesController : ControllerBase
{
    private readonly ISender _mediator;

    /// <inheritdoc />
    public GamesController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>返回全部已登记对战棋种的描述,按 <c>gameKey</c> 升序。</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GameDescriptorDto>>> Get(
        CancellationToken cancellationToken)
    {
        var items = await _mediator.Send(new GetGameDescriptorsQuery(), cancellationToken);
        return Ok(items);
    }
}
