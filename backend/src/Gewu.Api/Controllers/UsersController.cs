using System.IdentityModel.Tokens.Jwt;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Features.Rooms.GetMyActiveRooms;
using Gewu.Application.Features.Users.GetCurrentUser;
using Gewu.Application.Features.Users.GetUserGames;
using Gewu.Application.Features.Users.GetUserProfile;
using Gewu.Application.Features.Users.SearchUsers;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gewu.Api.Controllers;

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
        var userId = GetCurrentUserId();
        var dto = await _mediator.Send(new GetCurrentUserQuery(userId), cancellationToken);
        return Ok(dto);
    }

    /// <summary>
    /// 当前登录用户的活动房间列表(Waiting + Playing,作为玩家);供前端"继续对局"UI。
    /// 不含 Finished,不含围观。按 CreatedAt DESC 排序,不分页。
    /// </summary>
    [HttpGet("me/active-rooms")]
    public async Task<ActionResult<IReadOnlyList<RoomSummaryDto>>> MyActiveRooms(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var rooms = await _mediator.Send(new GetMyActiveRoomsQuery(userId), cancellationToken);
        return Ok(rooms);
    }

    private UserId GetCurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Missing sub claim.");
        return new UserId(Guid.Parse(sub));
    }

    /// <summary>
    /// 分页返回指定用户参与过的 Finished 对局战绩。任何登录用户可查看任何其他用户的战绩
    /// (公开原则,同 GitHub 公开仓库)。page 默认 1,pageSize 默认 20,pageSize 最大 100。
    /// </summary>
    [HttpGet("{id:guid}/games")]
    public async Task<ActionResult<PagedResult<UserGameSummaryDto>>> Games(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetUserGamesPagedQuery(new UserId(id), page, pageSize),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 按 Id 返回他人在某棋种上的公开主页(Rating / 战绩 / CreatedAt)。**不**含 Email。bot 账号也可查。
    /// <para>
    /// <paramref name="gameKey"/> 缺省 <c>gomoku</c>,**缺省只发生在这一层**。
    /// 该用户在该棋种上没有战绩行时返回初始值(1200 / 全 0)而不是 404 ——
    /// "这个人存在但没下过这个棋种"是正常答案。
    /// </para>
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserPublicProfileDto>> GetProfile(
        Guid id,
        [FromQuery] string? gameKey = null,
        CancellationToken cancellationToken = default)
    {
        var dto = await _mediator.Send(
            new GetUserProfileQuery(new UserId(id), gameKey ?? GameKeys.Gomoku), cancellationToken);
        return Ok(dto);
    }

    /// <summary>
    /// 按 Username 前缀(大小写不敏感)分页搜索真人。`search` 为空时返回所有真人按 Username ASC。
    /// bot 账号永远不在搜索结果。
    /// </summary>
    [HttpGet("")]
    public async Task<ActionResult<PagedResult<UserPublicProfileDto>>> Search(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new SearchUsersQuery(search, page, pageSize), cancellationToken);
        return Ok(result);
    }
}
