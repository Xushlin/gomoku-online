using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using MediatR;

namespace Gomoku.Application.Features.Users.GetLeaderboard;

/// <summary>
/// 拉取前 <see cref="LeaderboardSize"/> 位用户并映射为 <see cref="LeaderboardEntryDto"/>。
/// <c>Rank</c> 按仓储返回顺序从 1 递增分配(仓储 MUST 已按 <c>Rating DESC, Wins DESC, GamesPlayed ASC</c> 排好)。
/// </summary>
public sealed class GetLeaderboardQueryHandler
    : IRequestHandler<GetLeaderboardQuery, IReadOnlyList<LeaderboardEntryDto>>
{
    private const int LeaderboardSize = 100;

    private readonly IUserRepository _users;

    /// <inheritdoc />
    public GetLeaderboardQueryHandler(IUserRepository users)
    {
        _users = users;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LeaderboardEntryDto>> Handle(
        GetLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var users = await _users.GetTopByRatingAsync(LeaderboardSize, cancellationToken);

        return users
            .Select((u, i) => new LeaderboardEntryDto(
                Rank: i + 1,
                UserId: u.Id.Value,
                Username: u.Username.Value,
                Rating: u.Rating,
                GamesPlayed: u.GamesPlayed,
                Wins: u.Wins,
                Losses: u.Losses,
                Draws: u.Draws))
            .ToList()
            .AsReadOnly();
    }
}
