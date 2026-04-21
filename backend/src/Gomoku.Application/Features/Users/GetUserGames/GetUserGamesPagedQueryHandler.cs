using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Mapping;
using Gomoku.Domain.Rooms;
using MediatR;

namespace Gomoku.Application.Features.Users.GetUserGames;

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
            var whiteId = r.WhitePlayerId!.Value;
            return new UserGameSummaryDto(
                RoomId: r.Id.Value,
                Name: r.Name,
                Black: new UserSummaryDto(r.BlackPlayerId.Value, UserName(r.BlackPlayerId.Value)),
                White: new UserSummaryDto(whiteId.Value, UserName(whiteId.Value)),
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
