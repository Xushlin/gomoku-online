using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using MediatR;

namespace Gomoku.Application.Features.Users.GetCurrentUser;

/// <summary>
/// 从 JWT 取出 <c>sub</c> (UserId) 后查询用户。找不到 → <see cref="UserNotFoundException"/> (404);
/// <c>IsActive == false</c> → <see cref="UserNotActiveException"/> (403)。
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

        return user.ToDto();
    }
}
