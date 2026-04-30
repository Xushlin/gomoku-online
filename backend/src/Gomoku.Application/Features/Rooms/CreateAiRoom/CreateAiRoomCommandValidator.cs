using FluentValidation;
using Gomoku.Domain.Enums;

namespace Gomoku.Application.Features.Rooms.CreateAiRoom;

/// <summary>
/// <see cref="CreateAiRoomCommand"/> 校验器。规则与 <c>CreateRoomCommand</c> 对齐:
/// Name 非空,trim 后 3–50 字符。<c>Difficulty</c> 由枚举类型保证。
/// <c>HumanSide</c> 必须是 <see cref="Stone.Black"/> 或 <see cref="Stone.White"/> ——
/// <see cref="Stone.Empty"/> 显式拒绝(防止枚举默认值漏过来)。
/// </summary>
public sealed class CreateAiRoomCommandValidator : AbstractValidator<CreateAiRoomCommand>
{
    /// <summary>构造校验规则。</summary>
    public CreateAiRoomCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Room name is required.")
            .Must(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length >= 3 && n.Trim().Length <= 50)
            .WithMessage("Room name length must be between 3 and 50 characters.");

        RuleFor(x => x.HumanSide)
            .Must(s => s == Stone.Black || s == Stone.White)
            .WithMessage("HumanSide must be Black or White.");
    }
}
