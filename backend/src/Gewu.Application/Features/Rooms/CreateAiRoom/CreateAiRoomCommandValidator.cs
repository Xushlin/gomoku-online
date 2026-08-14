using FluentValidation;
using Gewu.Application.Common.Validation;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Features.Rooms.CreateAiRoom;

/// <summary>
/// <see cref="CreateAiRoomCommand"/> 校验器。规则与 <c>CreateRoomCommand</c> 对齐:
/// Name 非空,trim 后 3–50 字符;GameKey 必须是已登记的棋种。
/// <c>Difficulty</c> 由枚举类型保证。
/// <c>HumanSide</c> 必须是 <see cref="Stone.Black"/> 或 <see cref="Stone.White"/> ——
/// <see cref="Stone.Empty"/> 显式拒绝(防止枚举默认值漏过来)。
/// </summary>
public sealed class CreateAiRoomCommandValidator : AbstractValidator<CreateAiRoomCommand>
{
    /// <summary>构造校验规则。</summary>
    /// <param name="rules">棋种规则注册表 —— 判断"这是不是本平台的棋"的唯一真源。</param>
    public CreateAiRoomCommandValidator(IGameRulesRegistry rules)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Room name is required.")
            .Must(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length >= 3 && n.Trim().Length <= 50)
            .WithMessage("Room name length must be between 3 and 50 characters.");

        RuleFor(x => x.HumanSide)
            .Must(s => s == Stone.Black || s == Stone.White)
            .WithMessage("HumanSide must be Black or White.");

        RuleFor(x => x.GameKey).MustBeARegisteredGameKey(rules);
    }
}
