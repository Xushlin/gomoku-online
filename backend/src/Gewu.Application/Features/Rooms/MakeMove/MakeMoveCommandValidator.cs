using FluentValidation;

namespace Gewu.Application.Features.Rooms.MakeMove;

/// <summary>
/// 落子命令的入参粗校验:行列非负。
/// <para>
/// 这里**只**校验非负 —— 那条约束对任何棋种都成立。上界属于棋种(五子棋 15×15、
/// 一字棋 3×3),而校验器跑在解析房间之前,那时还不知道这是哪一种棋,所以上界由
/// <c>Room.PlayMove</c> 依据 <c>IGameRules</c> 判定。
/// </para>
/// <para>
/// 后果:`(20, 20)` 这类超界坐标从 400 变成 409。这是刻意的 ——
/// `(20, 20)` 是一个**格式良好**的请求,它只是在五子棋里不合法、在假想的 21×21 棋种里
/// 合法,所以"这一步在本局不合规"(409)比"你的请求有语法错"(400)更准确。
/// 负坐标仍是 400,因为它在任何棋种下都不成立。
/// </para>
/// </summary>
public sealed class MakeMoveCommandValidator : AbstractValidator<MakeMoveCommand>
{
    /// <summary>构造校验规则。</summary>
    public MakeMoveCommandValidator()
    {
        // 坐标**存在时**非负 —— 文本类的一步没有坐标,那不是错误。
        RuleFor(x => x.Row)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Row.HasValue)
            .WithMessage("Row must not be negative.");
        RuleFor(x => x.Col)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Col.HasValue)
            .WithMessage("Col must not be negative.");
        RuleFor(x => x.Text)
            .Must(t => !string.IsNullOrWhiteSpace(t))
            .When(x => x.Text is not null)
            .WithMessage("A spoken move must not be blank.");
    }
}
