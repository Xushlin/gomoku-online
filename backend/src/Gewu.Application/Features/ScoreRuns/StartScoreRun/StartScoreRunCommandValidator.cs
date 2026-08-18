using FluentValidation;

namespace Gewu.Application.Features.ScoreRuns.StartScoreRun;

/// <summary>
/// 开局入参的粗校验。**是否是一款计分类游戏不在这里判** —— 那个结果是 404,而
/// validator 的产出是 400。同 <c>StartPuzzleAttemptCommandValidator</c>。
/// </summary>
public sealed class StartScoreRunCommandValidator : AbstractValidator<StartScoreRunCommand>
{
    /// <summary>构造校验规则。</summary>
    public StartScoreRunCommandValidator()
    {
        RuleFor(x => x.GameKey)
            .NotEmpty().WithMessage("Game key is required.")
            .MaximumLength(64).WithMessage("Game key must not exceed 64 characters.");
    }
}
