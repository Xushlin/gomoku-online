using FluentValidation;

namespace Gewu.Application.Features.ScoreRuns.GetScoreLeaderboard;

/// <summary>
/// 校验:GameKey 非空;Window 是枚举里真有的值;Page ≥ 1;PageSize 在 [1, 100]。
/// <para>
/// <c>IsInEnum</c> 这条的理由被**实测改写过**,记下来因为原来那条是错的。我写的是
/// 「ASP.NET 的枚举绑定接受数字,所以 <c>?window=99</c> 会绑成 <c>(ScoreWindow)99</c>」——
/// 实测不是:查询串的枚举绑定**会**按已定义值校验。同一个构建上量出来的是
/// <c>0/1/2 → 200</c>、<c>3 / -1 / fortnight / 空 → 400</c>(400 来自模型绑定器,
/// 响应体是 RFC 9110 那个形状,不是本仓库的 <c>Validation failed.</c>),
/// 而名字大小写不敏感(<c>week</c> / <c>WEEK</c> / <c>Week</c> 都通)。
/// </para>
/// <para>
/// 所以这条规则**在这个端点上到不了**。它留下来的理由变了:校验的对象是**查询**而不是某一种
/// 传输,而 <c>GetScoreLeaderboardQuery</c> 可以由任何调用方构造(以后的 hub、后台任务、
/// 另一个 controller),那些路径没有模型绑定器。真正的防线是 <c>ScoreWindows.StartOf</c>
/// 现在对未定义值**抛**而不是当成 <c>all</c> —— 这条规则只负责把那个失败变成一个带字段名的 400。
/// </para>
/// <para>
/// **不校验 GameKey 是不是计分类游戏** —— 未登记的键返回空榜而不是 400,与 ELO 榜同一处理:
/// 集合端点上"这个游戏没有榜"与"榜是空的"对调用方无从分辨,而 400 会把前者说成客户端错了。
/// </para>
/// </summary>
public sealed class GetScoreLeaderboardQueryValidator : AbstractValidator<GetScoreLeaderboardQuery>
{
    /// <summary>构造校验规则。</summary>
    public GetScoreLeaderboardQueryValidator()
    {
        RuleFor(x => x.GameKey)
            .NotEmpty().WithMessage("GameKey must be non-empty.");
        RuleFor(x => x.Window)
            .IsInEnum().WithMessage("Window must be one of week, month, all.");
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be at least 1.");
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage("PageSize must be at least 1.")
            .LessThanOrEqualTo(100).WithMessage("PageSize must be at most 100.");
    }
}
