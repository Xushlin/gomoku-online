using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Features.Presence.GetOnlineCount;
using Gomoku.Application.Features.Presence.IsUserOnline;
using Gomoku.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gomoku.Api.Controllers;

/// <summary>
/// 在线状态查询。计数来自 <c>IConnectionTracker</c>(内存引用计数,同用户多连接算一个);
/// 前端大厅顶栏 / 用户主页在线徽章消费这两个端点。
/// </summary>
[ApiController]
[Route("api/presence")]
[Authorize]
public sealed class PresenceController : ControllerBase
{
    private readonly ISender _mediator;

    /// <inheritdoc />
    public PresenceController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>当前在线用户数(去重)。</summary>
    [HttpGet("online-count")]
    public async Task<ActionResult<OnlineCountDto>> OnlineCount(CancellationToken cancellationToken)
    {
        var dto = await _mediator.Send(new GetOnlineCountQuery(), cancellationToken);
        return Ok(dto);
    }

    /// <summary>指定用户是否在线。未知 UserId 仍返回 IsOnline=false,不 404。</summary>
    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<PresenceDto>> IsOnline(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _mediator.Send(new IsUserOnlineQuery(new UserId(id)), cancellationToken);
        return Ok(dto);
    }
}
