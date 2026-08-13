using FluentValidation;

namespace Gewu.Application.Features.Puzzles.StartPuzzleAttempt;

/// <summary>
/// 发起尝试入参的粗校验。游戏键**是否存在**不在这里判 —— 那是注册表的职责,
/// 且结果是 404 而不是 400:"这个游戏不存在"跟"你传的参数格式不对"是两件事。
/// </summary>
public sealed class StartPuzzleAttemptCommandValidator : AbstractValidator<StartPuzzleAttemptCommand>
{
    /// <summary>构造校验规则。</summary>
    public StartPuzzleAttemptCommandValidator()
    {
        RuleFor(x => x.GameKey)
            .NotEmpty().WithMessage("Game key is required.")
            .MaximumLength(64).WithMessage("Game key must not exceed 64 characters.");

        RuleFor(x => x.LevelIndex)
            .GreaterThanOrEqualTo(0).WithMessage("Level index must not be negative.");
    }
}
