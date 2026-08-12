using FluentValidation;

namespace Gomoku.Application.Features.Auth.Login;

/// <summary>
/// 仅做最小校验(非空)。**不**在 login 侧复用密码复杂度规则 —— 以免曾经注册时密码合规的老用户,
/// 因规则收紧而无法登录。
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    /// <summary>构造校验规则。</summary>
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}
