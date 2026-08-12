using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.Exceptions;
using MediatR;

namespace Gomoku.Application.Features.Auth.ChangePassword;

/// <summary>
/// 改密 handler。流程:Load → Verify 当前密码(错 → 401) → Hash 新密码 → User.ChangePassword
/// (Domain 校验:bot 拒绝)→ RevokeAllRefreshTokens(其它 session 失效)→ SaveChanges。
/// 返回 204。应用 / Domain 异常由全局中间件映射。
/// </summary>
public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Unit>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    /// <inheritdoc />
    public ChangePasswordCommandHandler(
        IUserRepository users,
        IPasswordHasher hasher,
        IDateTimeProvider clock,
        IUnitOfWork uow)
    {
        _users = users;
        _hasher = hasher;
        _clock = clock;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(request.UserId, cancellationToken)
            ?? throw new UserNotFoundException($"User '{request.UserId.Value}' was not found.");

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var newHash = _hasher.Hash(request.NewPassword);
        user.ChangePassword(newHash); // Bot 由 Domain 抛 InvalidOperationException
        user.RevokeAllRefreshTokens(_clock.UtcNow);

        await _uow.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
