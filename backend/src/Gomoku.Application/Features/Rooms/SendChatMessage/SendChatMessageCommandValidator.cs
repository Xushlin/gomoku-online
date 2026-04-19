using FluentValidation;
using Gomoku.Domain.Rooms;

namespace Gomoku.Application.Features.Rooms.SendChatMessage;

/// <summary>聊天命令入参粗校验:Content 非空、长度 1–500、Channel 是枚举定义值。</summary>
public sealed class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    /// <summary>构造校验规则。</summary>
    public SendChatMessageCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Chat content is required.")
            .Must(c => !string.IsNullOrWhiteSpace(c) && c.Trim().Length <= 500)
            .WithMessage("Chat content length must be between 1 and 500 characters after trim.");

        RuleFor(x => x.Channel)
            .IsInEnum().WithMessage("Channel must be a valid ChatChannel value.");
    }
}
