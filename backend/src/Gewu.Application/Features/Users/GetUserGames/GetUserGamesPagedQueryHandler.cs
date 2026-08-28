using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Mapping;
using Gewu.Domain.Rooms;
using MediatR;

namespace Gewu.Application.Features.Users.GetUserGames;

/// <summary>
/// 分页拉取用户战绩 handler。调 <see cref="IRoomRepository.GetUserFinishedGamesPagedAsync"/> 取
/// 分页 rooms + total,lookup usernames,映射为 <see cref="UserGameSummaryDto"/> 数组,
/// 包成 <see cref="PagedResult{T}"/> 返回。
/// </summary>
public sealed class GetUserGamesPagedQueryHandler
    : IRequestHandler<GetUserGamesPagedQuery, PagedResult<UserGameSummaryDto>>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;

    /// <inheritdoc />
    public GetUserGamesPagedQueryHandler(IRoomRepository rooms, IUserRepository users)
    {
        _rooms = rooms;
        _users = users;
    }

    /// <inheritdoc />
    public async Task<PagedResult<UserGameSummaryDto>> Handle(
        GetUserGamesPagedQuery request, CancellationToken cancellationToken)
    {
        var (rooms, total) = await _rooms.GetUserFinishedGamesPagedAsync(
            request.UserId, request.Page, request.PageSize, cancellationToken);

        if (rooms.Count == 0)
        {
            return new PagedResult<UserGameSummaryDto>(
                Array.Empty<UserGameSummaryDto>(),
                total,
                request.Page,
                request.PageSize);
        }

        // 合并所有房间的 UserId 以一次性 lookup(单用户通常就是 <= 20 行 × 2-3 id = 几十个)
        var allIds = rooms.SelectMany(r => r.CollectUserIds()).Distinct().ToList();
        var usernames = await _users.LookupUsernamesAsync(allIds, cancellationToken);

        string UserName(Guid id) => usernames.TryGetValue(id, out var n) ? n : "<unknown>";

        var items = rooms.Select(r =>
        {
            var game = r.Game!; // Finished 保证非 null(仓储 Where Status=Finished)
            return new UserGameSummaryDto(
                RoomId: r.Id.Value,
                Name: r.Name,
                // 走座位表 —— 与回放那条同一份投影。`BlackPlayerId` / `WhitePlayerId` 只认
                // 0 号与 1 号,而仓储不按棋种过滤,所以三座位对局会带着一个查不到的人进列表。
                Seats: r.ToSeatDtos(usernames),
                StartedAt: game.StartedAt,
                EndedAt: game.EndedAt!.Value,
                Result: game.Result!.Value,
                WinnerUserId: game.WinnerUserId?.Value,
                EndReason: game.EndReason!.Value,
                MoveCount: game.Moves.Count);
        }).ToList().AsReadOnly();

        return new PagedResult<UserGameSummaryDto>(items, total, request.Page, request.PageSize);
    }
}
