using System.IdentityModel.Tokens.Jwt;
using Gomoku.Api;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Features.Auth.ChangePassword;
using Gomoku.Application.Features.Auth.Login;
using Gomoku.Application.Features.Auth.Logout;
using Gomoku.Application.Features.Auth.RefreshToken;
using Gomoku.Application.Features.Auth.Register;
using Gomoku.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Gomoku.Api.Controllers;

/// <summary>注册 / 登录 / 刷新 / 登出 四个端点。</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ISender _mediator;

    /// <inheritdoc />
    public AuthController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>注册新用户并立即签发一对 token。成功返回 HTTP 201。</summary>
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitingOptions.AuthStrictPolicyName)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterCommand command,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>已有账号登录,签发一对 token。</summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitingOptions.AuthStrictPolicyName)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// 用 refresh token 换一对新 token;旧 refresh token 立即吊销(轮换)。
    /// 不需要 Authorization 头 —— refresh token 本身就是凭据。
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitingOptions.AuthStrictPolicyName)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        return Ok(response);
    }

    /// <summary>登出(幂等):吊销传入的 refresh token;无论是否找到都返回 204。</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// 修改当前登录用户的密码。要求提供当前密码做二次凭据验证;成功后全部 refresh token
    /// 立即吊销(其它设备 session 失效,用户要用新密码重登)。204 返回。
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest body,
        CancellationToken cancellationToken)
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Missing sub claim.");
        var userId = new UserId(Guid.Parse(sub));
        await _mediator.Send(
            new ChangePasswordCommand(userId, body.CurrentPassword, body.NewPassword),
            cancellationToken);
        return NoContent();
    }
}

/// <summary>POST /api/auth/change-password 的请求体。</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
