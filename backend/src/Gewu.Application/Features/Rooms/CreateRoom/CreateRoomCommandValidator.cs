using FluentValidation;
using Gewu.Application.Common.Validation;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Features.Rooms.CreateRoom;

/// <summary>
/// <see cref="CreateRoomCommand"/> 校验器:Name 非空,trim 后 3–50 字符;
/// GameKey 必须是已登记**且开放人人对战**的棋种;古谱线路 id 与棋种**必须同时给或同时不给**。
/// <para>
/// 第二条是本命令独有的 —— 它建的是真人房。<c>CreateAiRoomCommandValidator</c> 只有第一条。
/// </para>
/// </summary>
public sealed class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    /// <summary>构造校验规则。</summary>
    /// <param name="rules">棋种规则注册表 —— 判断"这是不是本平台的棋"的唯一真源。</param>
    public CreateRoomCommandValidator(IGameRulesRegistry rules)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Room name is required.")
            .Must(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length >= 3 && n.Trim().Length <= 50)
            .WithMessage("Room name length must be between 3 and 50 characters.");

        RuleFor(x => x.GameKey).MustBeARegisteredGameKey(rules);
        RuleFor(x => x.GameKey).MustSupportHumanVsHuman(rules);

        // ---- 线路 id 与棋种:两个方向 ----
        //
        // 判据取自**类型**(`IPositionalStartRules`),而不是把 `xiangqi-endgame` 这个键写进来。
        // 理由与本仓库反复付账的那条同一件事:一份手写的清单会与注册表漂,而这里漂的表现是
        // 「第二个从局面开局的棋种建房时被拒,而错误信息说的是别的事」。
        //
        // 键解析不出规则时两条都**静默通过** —— 那种情况由 MustBeARegisteredGameKey 报出。
        //
        // 「这条线路在不在」**不在这里查**,在 handler 里查。两处都查等于把同一个判断复制成
        // 两份,而这一份非有不可的是 handler 那份:它是「房间造出来之前必须先拒绝」这条
        // 保证本身,即使有人把下面三条规则全删了,也不会落地一局开局摆错的棋。

        RuleFor(x => x.ManualLineId)
            .Must((cmd, id) => id is null || Positional(rules, cmd.GameKey) is not false)
            .WithMessage(
                "A manual line id is only meaningful for a game that starts from a chosen position.");

        RuleFor(x => x.ManualLineId)
            .NotNull()
            .When(cmd => Positional(rules, cmd.GameKey) == true)
            .WithMessage("This game starts from a chosen position; a manual line id is required.");
    }

    /// <summary>
    /// 这个键是不是「从选定局面开局」的棋种;键解析不出规则时为 <c>null</c>。
    /// <para>
    /// 三态而不是布尔:「不是选定式」与「根本不是本平台的棋」要分开,否则后者会在这里
    /// 多报一条错误,而调用方会以为要改两个地方。
    /// </para>
    /// </summary>
    private static bool? Positional(IGameRulesRegistry rules, string? gameKey)
        => gameKey is null ? null : rules.For(gameKey) is { } r ? r is IPositionalStartRules : null;
}
