using FluentValidation;

namespace Gomoku.Application.Features.Auth.ChangePassword;

/// <summary>
/// <see cref="ChangePasswordCommand"/> 校验器:CurrentPassword 非空;NewPassword 复用
/// <c>RegisterCommandValidator</c> 的三条规则(≥ 8、含字母、含数字),两处同一标准。
/// </summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    /// <inheritdoc />
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Za-z]").WithMessage("Password must contain at least one letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
    }
}
