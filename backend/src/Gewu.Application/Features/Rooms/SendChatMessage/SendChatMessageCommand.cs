using Gewu.Application.Common.DTOs;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.SendChatMessage;

/// <summary>在房间里发一条聊天消息。</summary>
public sealed record SendChatMessageCommand(
    UserId UserId,
    RoomId RoomId,
    string Content,
    ChatChannel Channel) : IRequest<ChatMessageDto>;
