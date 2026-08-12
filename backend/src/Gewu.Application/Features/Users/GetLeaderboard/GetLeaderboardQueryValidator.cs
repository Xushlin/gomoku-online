using FluentValidation;

namespace Gomoku.Application.Features.Users.GetLeaderboard;

/// <summary>
/// <see cref="GetLeaderboardQuery"/> 校验:Page ≥ 1;PageSize 在 [1, 100]。
/// 与 <c>GetUserGamesPagedQueryValidator</c> 的风格对齐,非法 → <c>ValidationException</c> → 400。
/// </summary>
public sealed class GetLeaderboardQueryValidator : AbstractValidator<GetLeaderboardQuery>
{
    /// <inheritdoc />
    public GetLeaderboardQueryValidator()
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
