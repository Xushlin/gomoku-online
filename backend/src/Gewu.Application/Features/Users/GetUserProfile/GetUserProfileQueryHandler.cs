using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using MediatR;

namespace Gewu.Application.Features.Users.GetUserProfile;

/// <summary>
/// Load user,找不到抛 <see cref="UserNotFoundException"/>,否则取他在该棋种上的战绩行、
/// 映射为 <see cref="UserPublicProfileDto"/> 返回。不过滤 bot —— bot 也是"可公开查询"的账号。
/// <para>
/// 战绩行用 <c>FindGameStatsAsync</c> 取而**不是** get-or-create:一次 GET 请求把人凭空登记进
/// 某个棋种的排行榜,会把"下过"的含义变成"被人看过资料"。没有行就用初始值填 DTO ——
/// "这个人存在但没下过这个棋种"是正常答案,404 会被前端误报成"用户不存在"。
/// </para>
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

        var stats = await _users.FindGameStatsAsync(request.UserId, request.GameKey, cancellationToken);

        return user.ToPublicProfileDto(stats);
    }
}
