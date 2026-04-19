using FluentValidation;

namespace Gomoku.Application.Features.Rooms.CreateRoom;

/// <summary><see cref="CreateRoomCommand"/> 校验器:Name 非空,trim 后 3–50 字符。</summary>
public sealed class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    /// <summary>构造校验规则。</summary>
    public CreateRoomCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Room name is required.")
            .Must(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length >= 3 && n.Trim().Length <= 50)
            .WithMessage("Room name length must be between 3 and 50 characters.");
    }
}
