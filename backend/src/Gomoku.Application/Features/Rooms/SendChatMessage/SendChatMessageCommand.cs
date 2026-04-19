using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using MediatR;

namespace Gomoku.Application.Features.Rooms.SendChatMessage;

/// <summary>在房间里发一条聊天消息。</summary>
public sealed record SendChatMessageCommand(
    UserId UserId,
    RoomId RoomId,
    string Content,
    ChatChannel Channel) : IRequest<ChatMessageDto>;
