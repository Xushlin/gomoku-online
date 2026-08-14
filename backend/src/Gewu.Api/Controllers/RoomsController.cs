using System.IdentityModel.Tokens.Jwt;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Features.Rooms.CreateAiRoom;
using Gewu.Application.Features.Rooms.CreateRoom;
using Gewu.Application.Features.Rooms.Dissolve;
using Gewu.Application.Features.Rooms.GetGameReplay;
using Gewu.Application.Features.Rooms.Resign;
using Gewu.Application.Features.Rooms.GetRoomList;
using Gewu.Application.Features.Rooms.GetRoomState;
using Gewu.Application.Features.Rooms.JoinAsSpectator;
using Gewu.Application.Features.Rooms.JoinRoom;
using Gewu.Application.Features.Rooms.LeaveAsSpectator;
using Gewu.Application.Features.Rooms.LeaveRoom;
using Gewu.Domain.Ai;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gewu.Api.Controllers;

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
            new CreateRoomCommand(GetUserId(), body.Name, body.GameKey ?? GameKeys.Gomoku),
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
            new CreateAiRoomCommand(
                GetUserId(),
                body.Name,
                body.Difficulty,
                body.HumanSide ?? Stone.Black,
                body.GameKey ?? GameKeys.Gomoku),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = state.Id }, state);
    }

    /// <summary>
    /// 列出某个棋种下所有活跃(Waiting / Playing)房间。
    /// <para>
    /// <c>gameKey</c> 可省略,缺省 <c>gomoku</c>(向后兼容,见 <see cref="CreateRoomRequest"/>)。
    /// 未登记的棋种返回空列表 + 200,不是错误 —— 集合端点上"没有这种房间"与"没有这个
    /// 棋种"对调用方无从分辨。
    /// </para>
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoomSummaryDto>>> List(
        [FromQuery] string? gameKey,
        CancellationToken cancellationToken)
    {
        var rooms = await _mediator.Send(
            new GetRoomListQuery(gameKey ?? GameKeys.Gomoku), cancellationToken);
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

    /// <summary>
    /// 解散房间(Host 专属)。仅 Waiting 状态允许;成功后房间物理删除,围观者会收到
    /// SignalR <c>RoomDissolved</c> 事件。Playing 状态请走认输 / 超时路径。
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Dissolve(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DissolveRoomCommand(GetUserId(), new RoomId(id)), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// 玩家主动认输。仅 Playing 状态允许;允许**任意回合**(含对手回合)。返回对局结束 DTO,
    /// 含 <c>EndReason = Resigned</c>。同事务内写入双方 ELO 变动。
    /// </summary>
    [HttpPost("{id:guid}/resign")]
    public async Task<ActionResult<GameEndedDto>> Resign(Guid id, CancellationToken cancellationToken)
    {
        var ended = await _mediator.Send(new ResignCommand(GetUserId(), new RoomId(id)), cancellationToken);
        return Ok(ended);
    }

    /// <summary>
    /// 按房间 Id 拉取 Finished 对局的完整回放(Moves 按 Ply 升序)。任何登录用户可访问。
    /// Playing / Waiting 房间请求此端点返回 409。
    /// </summary>
    [HttpGet("{id:guid}/replay")]
    public async Task<ActionResult<GameReplayDto>> Replay(Guid id, CancellationToken cancellationToken)
    {
        var dto = await _mediator.Send(new GetGameReplayQuery(new RoomId(id)), cancellationToken);
        return Ok(dto);
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
/// <summary>
/// POST /api/rooms 的请求体。
/// <para>
/// <c>GameKey</c> 可空 —— 缺省 / null 时 controller 填 <c>gomoku</c>。这是本变更**唯一**
/// 的缺省值妥协,理由见 <c>add-tictactoe</c> design D3:本变更不含 Web 客户端,已发布的
/// 客户端不会送这个字段,让它们从此建不出房是不可接受的回归。缺省只活在这一层,
/// <c>CreateRoomCommand.GameKey</c> 是必填的非空字段。
/// </para>
/// <para>
/// 未登记的棋种由 application validator 拒绝(HTTP 400)。
/// </para>
/// </summary>
public sealed record CreateRoomRequest(string Name, string? GameKey = null);

/// <summary>
/// POST /api/rooms/ai 的请求体。<c>Difficulty</c> 以字符串形式(JsonStringEnumConverter)。
/// <c>HumanSide</c> 可空 —— 缺省 / null 时 controller 默认填 <c>Stone.Black</c>(向后兼容,
/// 旧客户端继续工作)。显式 <c>"Black"</c> / <c>"White"</c> 让真人选边;<c>"Empty"</c> 等其它
/// 值由 application validator 拒绝(HTTP 400)。
/// <para>
/// <c>GameKey</c> 同样可空,缺省填 <c>gomoku</c> —— 同一个理由,见 <see cref="CreateRoomRequest"/>。
/// </para>
/// </summary>
public sealed record CreateAiRoomRequest(
    string Name,
    BotDifficulty Difficulty,
    Stone? HumanSide = null,
    string? GameKey = null);
