using FluentValidation;

namespace Gewu.Application.Features.Puzzles.CheckPuzzlePartial;

/// <summary>
/// 部分校验入参的粗校验。
/// <para>
/// 只拦"明显不是提交内容"的输入(空、超长),**不**尝试理解 JSON 结构 ——
/// 内容对平台是不透明的,懂它的是各游戏的 <c>IPuzzleRules</c>。上限存在的意义是
/// 让一个荒谬体积的请求在进 handler 前就被拒掉。
/// </para>
/// </summary>
public sealed class CheckPuzzlePartialCommandValidator : AbstractValidator<CheckPuzzlePartialCommand>
{
    /// <summary>提交内容长度上限(字符)。</summary>
    public const int MaxPayloadLength = 8_000;

    /// <summary>构造校验规则。</summary>
    public CheckPuzzlePartialCommandValidator()
    {
        RuleFor(x => x.AttemptId)
            .NotEmpty().WithMessage("Attempt id is required.");

        RuleFor(x => x.PartialJson)
            .NotEmpty().WithMessage("Partial submission is required.")
            .MaximumLength(MaxPayloadLength)
            .WithMessage($"Partial submission must not exceed {MaxPayloadLength} characters.");
    }
}
