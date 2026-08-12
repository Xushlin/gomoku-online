using FluentValidation;
using Gomoku.Domain.ValueObjects;

namespace Gomoku.Application.Features.Rooms.MakeMove;

/// <summary>落子命令的入参粗校验:行列在 [0..14] 范围内。</summary>
public sealed class MakeMoveCommandValidator : AbstractValidator<MakeMoveCommand>
{
    /// <summary>构造校验规则。</summary>
    public MakeMoveCommandValidator()
    {
        RuleFor(x => x.Row)
            .InclusiveBetween(0, Position.MaxIndex)
            .WithMessage("Row must be in [0..14].");
        RuleFor(x => x.Col)
            .InclusiveBetween(0, Position.MaxIndex)
            .WithMessage("Col must be in [0..14].");
    }
}
