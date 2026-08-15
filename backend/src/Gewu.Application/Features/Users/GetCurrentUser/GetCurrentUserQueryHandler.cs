using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using Gewu.Domain.Games.Abstractions;
using MediatR;

namespace Gewu.Application.Features.Users.GetCurrentUser;

/// <summary>
/// 从 JWT 取出 <c>sub</c> (UserId) 后查询用户。找不到 → <see cref="UserNotFoundException"/> (404);
/// <c>IsActive == false</c> → <see cref="UserNotActiveException"/> (403)。
/// <para>
/// <c>UserDto</c> 的战绩四项与 Rating 钉在**五子棋**。`/api/users/me` 没有 `gameKey` 参数,
/// 而已发布的客户端在这里读的就是五子棋的数字 —— 换成别的会是一次无声的回归。
/// "`/me` 返回全部棋种的战绩"要改 DTO 形状,属于 `add-web-per-game-rating` 那一步。
/// </para>
/// </summary>
public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, UserDto>
{
    private readonly IUserRepository _users;

    /// <inheritdoc />
    public GetCurrentUserQueryHandler(IUserRepository users)
    {
        _users = users;
    }

    /// <inheritdoc />
    public async Task<UserDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            throw new UserNotFoundException($"User '{request.UserId.Value}' was not found.");
        }

        if (!user.IsActive)
        {
            throw new UserNotActiveException($"User '{user.Username.Value}' is not active.");
        }

        var stats = await _users.FindGameStatsAsync(request.UserId, GameKeys.Gomoku, cancellationToken);

        return user.ToDto(stats);
    }
}
