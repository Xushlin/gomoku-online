using FluentValidation;

namespace Gomoku.Application.Features.Users.GetUserGames;

/// <summary>
/// <see cref="GetUserGamesPagedQuery"/> 的分页参数校验。
/// Page ≥ 1;PageSize 在 [1, 100]。非法 → <see cref="Gomoku.Application.Common.Exceptions.ValidationException"/>
/// → HTTP 400。
/// </summary>
public sealed class GetUserGamesPagedQueryValidator : AbstractValidator<GetUserGamesPagedQuery>
{
    /// <inheritdoc />
    public GetUserGamesPagedQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1.");
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be at least 1.")
            .LessThanOrEqualTo(100)
            .WithMessage("PageSize must be at most 100.");
    }
}
