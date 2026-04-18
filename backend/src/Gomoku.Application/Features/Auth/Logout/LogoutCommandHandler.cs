using Gomoku.Application.Abstractions;
using MediatR;

namespace Gomoku.Application.Features.Auth.Logout;

/// <summary>
/// 登出 handler。行为幂等:任何"找不到用户 / 找不到 token / token 已吊销"都**静默成功**
/// 返回 <see cref="Unit.Value"/>,避免通过响应码探测 token 有效性,也避免客户端本地清凭据后
/// 服务端还抛 4xx 的噪音。MUST NOT 打印包含原始 token 的日志。
/// </summary>
public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    /// <inheritdoc />
    public LogoutCommandHandler(
        IUserRepository users,
        IJwtTokenService tokens,
        IDateTimeProvider clock,
        IUnitOfWork uow)
    {
        _users = users;
        _tokens = tokens;
        _clock = clock;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Unit.Value;
        }

        var hash = _tokens.HashRefreshToken(request.RefreshToken);
        var user = await _users.FindByRefreshTokenHashAsync(hash, cancellationToken);
        if (user is null)
        {
            return Unit.Value;
        }

        var revoked = user.RevokeRefreshToken(hash, _clock.UtcNow);
        if (revoked)
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return Unit.Value;
    }
}
