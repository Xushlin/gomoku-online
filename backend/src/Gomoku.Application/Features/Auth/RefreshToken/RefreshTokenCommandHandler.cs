using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gomoku.Application.Features.Auth.RefreshToken;

/// <summary>
/// Refresh Token 轮换流程:hash 入参 → 查用户 → 定位对应子实体并校验 <c>IsActive(now)</c> →
/// 吊销旧 token → 发放新 token → SaveChanges → 签发新 access token → 返回响应。
/// 任何失败路径都抛 <see cref="InvalidRefreshTokenException"/>(HTTP 401)。
/// </summary>
public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly JwtOptions _jwtOptions;

    /// <inheritdoc />
    public RefreshTokenCommandHandler(
        IUserRepository users,
        IJwtTokenService tokens,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _tokens = tokens;
        _clock = clock;
        _uow = uow;
        _jwtOptions = jwtOptions.Value;
    }

    /// <inheritdoc />
    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var incomingHash = _tokens.HashRefreshToken(request.RefreshToken);

        var user = await _users.FindByRefreshTokenHashAsync(incomingHash, cancellationToken);
        if (user is null)
        {
            throw new InvalidRefreshTokenException();
        }

        var now = _clock.UtcNow;
        var existing = user.RefreshTokens.FirstOrDefault(t => t.TokenHash == incomingHash);
        if (existing is null || !existing.IsActive(now))
        {
            throw new InvalidRefreshTokenException();
        }

        user.RevokeRefreshToken(incomingHash, now);

        var rawNew = _tokens.GenerateRefreshToken();
        var newHash = _tokens.HashRefreshToken(rawNew);
        var newExpiresAt = now.AddDays(_jwtOptions.RefreshTokenLifetimeDays);
        user.IssueRefreshToken(newHash, newExpiresAt, now);

        await _uow.SaveChangesAsync(cancellationToken);

        var accessToken = _tokens.GenerateAccessToken(user);

        return new AuthResponse(
            AccessToken: accessToken.Token,
            RefreshToken: rawNew,
            AccessTokenExpiresAt: accessToken.ExpiresAt,
            User: user.ToDto());
    }
}
