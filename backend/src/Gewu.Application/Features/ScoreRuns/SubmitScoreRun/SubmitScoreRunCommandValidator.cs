using FluentValidation;

namespace Gewu.Application.Features.ScoreRuns.SubmitScoreRun;

/// <summary>
/// 提交入参的粗校验:run id 非空、放置序列非空且不超上限、每个放置的旋转与列在场地范围内。
/// <para>
/// 旋转态与列的范围在这里**只做粗筛**:一个形状在某个列到底放不放得下,取决于它的宽度与
/// 那一刻的场地,那是规则的事。validator 拦的是"这个数根本不可能对"(负数、超出 0–3 / 0–9),
/// 好让明显的坏输入拿到 400 与字段名,而不是一句"放置 137 非法"。
/// </para>
/// </summary>
public sealed class SubmitScoreRunCommandValidator : AbstractValidator<SubmitScoreRunCommand>
{
    /// <summary>
    /// 放置数上限。
    /// <para>
    /// **这不是分数上限** —— 分数刻意不设上限(硬上限会先误伤真高手)。这是一条资源限制:
    /// 请求体与重放都是 O(n),没有上限时一个 100 万条的序列就是一次免费的 CPU 与带宽消耗。
    /// 取 10 万的算术依据:每个方块按 2 秒算,10 万次放置是**连续玩 55 小时**,
    /// 真玩家碰不到这个数。
    /// </para>
    /// </summary>
    public const int MaxPlacements = 100_000;

    /// <summary>旋转态上限(含)。</summary>
    public const int MaxRotation = 3;

    /// <summary>构造校验规则。</summary>
    public SubmitScoreRunCommandValidator()
    {
        RuleFor(x => x.RunId)
            .NotEmpty().WithMessage("Run id is required.");

        RuleFor(x => x.Placements)
            .NotNull().WithMessage("Placements are required.")
            .NotEmpty().WithMessage("Placements must not be empty.")
            .Must(p => p is null || p.Count <= MaxPlacements)
            .WithMessage($"Placements must not exceed {MaxPlacements} entries.");

        RuleForEach(x => x.Placements).ChildRules(p =>
        {
            p.RuleFor(x => x.Rotation)
                .InclusiveBetween(0, MaxRotation)
                .WithMessage($"Rotation must be between 0 and {MaxRotation}.");
            p.RuleFor(x => x.Column)
                .InclusiveBetween(0, Gewu.Domain.Games.Tetris.TetrisRules.Columns - 1)
                .WithMessage($"Column must be between 0 and {Gewu.Domain.Games.Tetris.TetrisRules.Columns - 1}.");
        });
    }
}
