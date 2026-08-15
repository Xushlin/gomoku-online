using FluentValidation;

namespace Gewu.Application.Features.Users.GetLeaderboard;

/// <summary>
/// <see cref="GetLeaderboardQuery"/> 校验:GameKey 非空;Page ≥ 1;PageSize 在 [1, 100]。
/// 与 <c>GetUserGamesPagedQueryValidator</c> 的风格对齐,非法 → <c>ValidationException</c> → 400。
/// <para>
/// **不校验 GameKey 是否已登记** —— 未登记的棋种返回空榜而不是 400,与房间列表同一处理:
/// 集合端点上"这个棋种没有榜"与"榜是空的"对调用方无从分辨,而 400 会把前者说成客户端错了。
/// </para>
/// </summary>
public sealed class GetLeaderboardQueryValidator : AbstractValidator<GetLeaderboardQuery>
{
    /// <inheritdoc />
    public GetLeaderboardQueryValidator()
    {
        RuleFor(x => x.GameKey)
            .NotEmpty()
            .WithMessage("GameKey must be non-empty.");
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
