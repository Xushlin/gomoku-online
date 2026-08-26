using Gewu.Application.Common.DTOs;
using Gewu.Application.Features.Manuals.GetXiangqiManual;
using Gewu.Application.Features.Manuals.GetXiangqiManualLine;
using Gewu.Application.Features.Manuals.ListXiangqiManuals;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gewu.Api.Controllers;

/// <summary>
/// 古谱 —— 只读资料。
/// <para>
/// **匿名可读**,而这与回放端点要求身份不矛盾:回放暴露的是**具体用户的对局**,
/// 古谱是一部三百年前的公开著作。
/// </para>
/// </summary>
[ApiController]
[Route("api/manuals")]
[AllowAnonymous]
public sealed class ManualsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>构造控制器。</summary>
    /// <param name="mediator">MediatR。</param>
    public ManualsController(IMediator mediator) => _mediator = mediator;

    /// <summary>列出全部象棋古谱。</summary>
    /// <param name="ct">取消标记。</param>
    /// <returns>每部谱及其条数。</returns>
    [HttpGet("xiangqi")]
    [ProducesResponseType(typeof(IReadOnlyList<ManualSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ManualSummaryDto>>> ListManuals(CancellationToken ct)
        => Ok(await _mediator.Send(new ListXiangqiManualsQuery(), ct));

    /// <summary>取一部象棋古谱的目录。</summary>
    /// <param name="manualKey">古谱键,例如 meihuapu。</param>
    /// <param name="ct">取消标记。</param>
    /// <returns>按局分组的目录。</returns>
    [HttpGet("xiangqi/{manualKey}")]
    [ProducesResponseType(typeof(ManualCatalogueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManualCatalogueDto>> GetCatalogue(
        string manualKey, CancellationToken ct)
    {
        var catalogue = await _mediator.Send(new GetXiangqiManualQuery(manualKey), ct);
        return catalogue is null ? NotFound() : Ok(catalogue);
    }

    /// <summary>取一条古谱线路。</summary>
    /// <param name="lineId">线路主键。</param>
    /// <param name="ct">取消标记。</param>
    /// <returns>该条线路,不存在时 404。</returns>
    [HttpGet("xiangqi/lines/{lineId:int}")]
    [ProducesResponseType(typeof(ManualLineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManualLineDto>> GetLine(int lineId, CancellationToken ct)
    {
        var line = await _mediator.Send(new GetXiangqiManualLineQuery(lineId), ct);
        return line is null ? NotFound() : Ok(line);
    }
}
