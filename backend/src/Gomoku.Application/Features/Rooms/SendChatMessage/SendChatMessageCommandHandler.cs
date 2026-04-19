using Gomoku.Application.Abstractions;
using Gomoku.Application.Common.DTOs;
using Gomoku.Application.Common.Exceptions;
using MediatR;

namespace Gomoku.Application.Features.Rooms.SendChatMessage;

/// <summary>聊天 handler。查发送者的 username snapshot,调 <c>Room.PostChatMessage</c>,按频道推送。</summary>
public sealed class SendChatMessageCommandHandler : IRequestHandler<SendChatMessageCommand, ChatMessageDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly IRoomNotifier _notifier;

    /// <inheritdoc />
    public SendChatMessageCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IRoomNotifier notifier)
    {
        _rooms = rooms;
        _users = users;
        _clock = clock;
        _uow = uow;
        _notifier = notifier;
    }

    /// <inheritdoc />
    public async Task<ChatMessageDto> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken)
            ?? throw new RoomNotFoundException($"Room '{request.RoomId.Value}' was not found.");

        var sender = await _users.FindByIdAsync(request.UserId, cancellationToken)
            ?? throw new UserNotFoundException($"User '{request.UserId.Value}' was not found.");

        var message = room.PostChatMessage(
            request.UserId,
            sender.Username.Value,
            request.Content,
            request.Channel,
            _clock.UtcNow);

        await _uow.SaveChangesAsync(cancellationToken);

        var dto = new ChatMessageDto(
            message.Id,
            message.SenderUserId.Value,
            message.SenderUsername,
            message.Content,
            message.Channel,
            message.SentAt);

        await _notifier.ChatMessagePostedAsync(room.Id, message.Channel, dto, cancellationToken);
        return dto;
    }
}
