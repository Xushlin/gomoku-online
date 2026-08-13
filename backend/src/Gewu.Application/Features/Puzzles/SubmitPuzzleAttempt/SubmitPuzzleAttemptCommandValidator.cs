using FluentValidation;

namespace Gewu.Application.Features.Puzzles.SubmitPuzzleAttempt;

/// <summary>
/// 提交入参的粗校验 —— 同 <c>CheckPuzzlePartialCommandValidator</c>:只拦空与超长,
/// 不解读结构。上限比部分校验宽,因为完整答案自然更大(整张网格 vs 一条成语)。
/// </summary>
public sealed class SubmitPuzzleAttemptCommandValidator : AbstractValidator<SubmitPuzzleAttemptCommand>
{
    /// <summary>完整答案长度上限(字符)。</summary>
    public const int MaxPayloadLength = 64_000;

    /// <summary>构造校验规则。</summary>
    public SubmitPuzzleAttemptCommandValidator()
    {
        RuleFor(x => x.AttemptId)
            .NotEmpty().WithMessage("Attempt id is required.");

        RuleFor(x => x.SubmissionJson)
            .NotEmpty().WithMessage("Submission is required.")
            .MaximumLength(MaxPayloadLength)
            .WithMessage($"Submission must not exceed {MaxPayloadLength} characters.");
    }
}
