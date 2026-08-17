using FluentValidation;
using Gewu.Application.Common.Validation;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Application.Features.Rooms.CreateRoom;

/// <summary>
/// <see cref="CreateRoomCommand"/> 校验器:Name 非空,trim 后 3–50 字符;
/// GameKey 必须是已登记**且开放人人对战**的棋种。
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
    }
}
