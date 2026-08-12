using FluentValidation;

namespace Gomoku.Application.Features.Users.SearchUsers;

/// <summary>
/// Page / PageSize 校验与 <c>GetUserGamesPagedQueryValidator</c> 对齐。Search 可空,非空
/// 时长度 ≤ 20(与 <c>Username</c> 的最大长度对齐)。
/// </summary>
public sealed class SearchUsersQueryValidator : AbstractValidator<SearchUsersQuery>
{
    /// <inheritdoc />
    public SearchUsersQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1.");
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be at least 1.")
            .LessThanOrEqualTo(100)
            .WithMessage("PageSize must be at most 100.");
        RuleFor(x => x.Search)
            .MaximumLength(20)
            .When(x => !string.IsNullOrEmpty(x.Search))
            .WithMessage("Search prefix must be at most 20 characters.");
    }
}
