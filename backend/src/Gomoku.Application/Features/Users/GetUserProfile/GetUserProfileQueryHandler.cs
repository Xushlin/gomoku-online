using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using MediatR;

namespace Gomoku.Application.Features.Users.GetUserProfile;

/// <summary>
/// Load user,找不到抛 <see cref="UserNotFoundException"/>,否则映射为
/// <see cref="UserPublicProfileDto"/> 返回。不过滤 bot —— bot 也是"可公开查询"的账号。
/// </summary>
public sealed class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, UserPublicProfileDto>
{
    private readonly IUserRepository _users;

    /// <inheritdoc />
    public GetUserProfileQueryHandler(IUserRepository users)
    {
        _users = users;
    }

    /// <inheritdoc />
    public async Task<UserPublicProfileDto> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(request.UserId, cancellationToken)
            ?? throw new UserNotFoundException($"User '{request.UserId.Value}' was not found.");

        return new UserPublicProfileDto(
            Id: user.Id.Value,
            Username: user.Username.Value,
            Rating: user.Rating,
            GamesPlayed: user.GamesPlayed,
            Wins: user.Wins,
            Losses: user.Losses,
            Draws: user.Draws,
            CreatedAt: user.CreatedAt);
    }
}
