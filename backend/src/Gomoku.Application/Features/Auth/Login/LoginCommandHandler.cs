using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using Gomoku.Application.Common.Mapping;
using Gomoku.Domain.Users;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gomoku.Application.Features.Auth.Login;

/// <summary>
/// 登录流程:构造 <see cref="Email"/> → 查 user → 校验密码 → 检查启用状态 →
/// 签发一枚新 refresh token 加入聚合 → 返回 <see cref="AuthResponse"/>。
/// "用户不存在"与"密码错误"**抛同一个** <see cref="InvalidCredentialsException"/>,
/// 消息完全一致,避免泄漏邮箱是否已注册。
/// </summary>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly JwtOptions _jwtOptions;

    /// <inheritdoc />
    public LoginCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IJwtTokenService tokens,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _clock = clock;
        _uow = uow;
        _jwtOptions = jwtOptions.Value;
    }

    /// <inheritdoc />
    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        Email email;
        try
        {
            email = new Email(request.Email);
        }
        catch (Gomoku.Domain.Exceptions.InvalidEmailException)
        {
            // 邮箱格式非法也回同样的模糊错误,不暴露校验细节。
            throw new InvalidCredentialsException();
        }

        var user = await _users.FindByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            throw new InvalidCredentialsException();
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        if (!user.IsActive)
        {
            throw new UserNotActiveException($"User '{user.Username.Value}' is not active.");
        }

        var now = _clock.UtcNow;
        var rawRefreshToken = _tokens.GenerateRefreshToken();
        var refreshHash = _tokens.HashRefreshToken(rawRefreshToken);
        var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenLifetimeDays);
        user.IssueRefreshToken(refreshHash, refreshExpiresAt, now);

        await _uow.SaveChangesAsync(cancellationToken);

        var accessToken = _tokens.GenerateAccessToken(user);

        return new AuthResponse(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            AccessTokenExpiresAt: accessToken.ExpiresAt,
            User: user.ToDto());
    }
}
