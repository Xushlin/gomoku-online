using FluentValidation;

namespace Gomoku.Application.Features.Auth.RefreshToken;

/// <summary><see cref="RefreshTokenCommand"/> 的基础校验:<c>RefreshToken</c> 非空。</summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    /// <summary>构造校验规则。</summary>
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
