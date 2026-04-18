using System.IdentityModel.Tokens.Jwt;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Features.Users.GetCurrentUser;
using Gomoku.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gomoku.Api.Controllers;

/// <summary>当前用户信息查询。其他用户资料编辑 / 头像等留给后续变更。</summary>
[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly ISender _mediator;

    /// <inheritdoc />
    public UsersController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>返回当前登录用户的 <see cref="UserDto"/>。</summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Missing sub claim.");

        var userId = new UserId(Guid.Parse(sub));
        var dto = await _mediator.Send(new GetCurrentUserQuery(userId), cancellationToken);
        return Ok(dto);
    }
}
