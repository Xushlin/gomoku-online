using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using Gewu.Domain.Users;
using MediatR;
using Microsoft.Extensions.Options;

namespace Gewu.Application.Features.Auth.Register;

/// <summary>
/// 注册流程:构造 <see cref="Email"/> / <see cref="Username"/>(格式由值对象校验) →
/// 唯一性预检 → 哈希密码 → <see cref="User.Register"/> → 签发一对 token(原始 refresh token
/// 只在响应体出现,数据库存 hash) → <see cref="IUnitOfWork.SaveChangesAsync"/>。
/// </summary>
public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly JwtOptions _jwtOptions;

    /// <inheritdoc />
    public RegisterCommandHandler(
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
    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var email = new Email(request.Email);
        var username = new Username(request.Username);

        if (await _users.EmailExistsAsync(email, cancellationToken))
        {
            throw new EmailAlreadyExistsException($"Email '{email.Value}' is already registered.");
        }

        if (await _users.UsernameExistsAsync(username, cancellationToken))
        {
            throw new UsernameAlreadyExistsException($"Username '{username.Value}' is already taken.");
        }

        var now = _clock.UtcNow;
        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Register(UserId.NewId(), email, username, passwordHash, now);

        var rawRefreshToken = _tokens.GenerateRefreshToken();
        var refreshHash = _tokens.HashRefreshToken(rawRefreshToken);
        var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenLifetimeDays);
        user.IssueRefreshToken(refreshHash, refreshExpiresAt, now);

        await _users.AddAsync(user, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var accessToken = _tokens.GenerateAccessToken(user);

        return new AuthResponse(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            AccessTokenExpiresAt: accessToken.ExpiresAt,
            // 刚注册的用户在每个棋种上都还没下过 —— 没有战绩行,DTO 用初始值填。
            // 这里 MUST NOT 建行:"有行"就是"下完过",注册不该把人凭空登记进五子棋排行榜。
            User: user.ToDto(stats: null));
    }
}
