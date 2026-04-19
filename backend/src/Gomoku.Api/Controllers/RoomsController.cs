using System.IdentityModel.Tokens.Jwt;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Features.Rooms.CreateAiRoom;
using Gomoku.Application.Features.Rooms.CreateRoom;
using Gomoku.Application.Features.Rooms.GetRoomList;
using Gomoku.Application.Features.Rooms.GetRoomState;
using Gomoku.Application.Features.Rooms.JoinAsSpectator;
using Gomoku.Application.Features.Rooms.JoinRoom;
using Gomoku.Application.Features.Rooms.LeaveAsSpectator;
using Gomoku.Application.Features.Rooms.LeaveRoom;
using Gomoku.Domain.Ai;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gomoku.Api.Controllers;

/// <summary>房间聚合的 REST 接口。落子 / 聊天 / 催促走 SignalR,不在此处。</summary>
[ApiController]
[Route("api/rooms")]
[Authorize]
public sealed class RoomsController : ControllerBase
{
    private readonly ISender _mediator;

    /// <inheritdoc />
    public RoomsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>创建一个房间。调用方成为 Host 与黑方。</summary>
    [HttpPost]
    public async Task<ActionResult<RoomSummaryDto>> Create(
        [FromBody] CreateRoomRequest body,
        CancellationToken cancellationToken)
    {
        var summary = await _mediator.Send(
            new CreateRoomCommand(GetUserId(), body.Name),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = summary.Id }, summary);
    }

    /// <summary>
    /// 创建一个 AI 对局房间。调用方成为 Host + 黑方;seeded 机器人按 <c>difficulty</c>
    /// 立即加入为白方。返回的 <see cref="RoomStateDto"/> 状态已是 Playing。
    /// </summary>
    [HttpPost("ai")]
    public async Task<ActionResult<RoomStateDto>> CreateAi(
        [FromBody] CreateAiRoomRequest body,
        CancellationToken cancellationToken)
    {
        var state = await _mediator.Send(
            new CreateAiRoomCommand(GetUserId(), body.Name, body.Difficulty),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = state.Id }, state);
    }

    /// <summary>列出所有活跃(Waiting / Playing)房间。</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoomSummaryDto>>> List(CancellationToken cancellationToken)
    {
        var rooms = await _mediator.Send(new GetRoomListQuery(), cancellationToken);
        return Ok(rooms);
    }

    /// <summary>获取房间完整状态(含所有 Moves / Chat / Spectators)。</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoomStateDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var state = await _mediator.Send(new GetRoomStateQuery(new RoomId(id)), cancellationToken);
        return Ok(state);
    }

    /// <summary>作为白方加入房间,触发对局启动。</summary>
    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<RoomStateDto>> Join(Guid id, CancellationToken cancellationToken)
    {
        var state = await _mediator.Send(new JoinRoomCommand(GetUserId(), new RoomId(id)), cancellationToken);
        return Ok(state);
    }

    /// <summary>离开房间(玩家离席或围观者离开)。</summary>
    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new LeaveRoomCommand(GetUserId(), new RoomId(id)), cancellationToken);
        return NoContent();
    }

    /// <summary>加入围观。</summary>
    [HttpPost("{id:guid}/spectate")]
    public async Task<IActionResult> Spectate(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new JoinAsSpectatorCommand(GetUserId(), new RoomId(id)), cancellationToken);
        return NoContent();
    }

    /// <summary>离开围观。</summary>
    [HttpDelete("{id:guid}/spectate")]
    public async Task<IActionResult> Unspectate(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new LeaveAsSpectatorCommand(GetUserId(), new RoomId(id)), cancellationToken);
        return NoContent();
    }

    private UserId GetUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Missing sub claim.");
        return new UserId(Guid.Parse(sub));
    }
}

/// <summary>POST /api/rooms 的请求体。</summary>
public sealed record CreateRoomRequest(string Name);

/// <summary>POST /api/rooms/ai 的请求体。<c>Difficulty</c> 以字符串形式(JsonStringEnumConverter)。</summary>
public sealed record CreateAiRoomRequest(string Name, BotDifficulty Difficulty);
