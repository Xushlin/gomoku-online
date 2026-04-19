using Gomoku.Domain.Users;

namespace Gomoku.Domain.Rooms;

/// <summary>
/// 房间内一条已发送的聊天消息。由 <see cref="Room.PostChatMessage"/> 内部构造。
/// <see cref="SenderUsername"/> 是发送时的 snapshot,用户改名后历史消息保留旧名。
/// </summary>
public sealed class ChatMessage
{
    /// <summary>子实体主键。</summary>
    public Guid Id { get; private set; }

    /// <summary>所属房间 Id。</summary>
    public RoomId RoomId { get; private set; }

    /// <summary>发送者用户 Id。</summary>
    public UserId SenderUserId { get; private set; }

    /// <summary>发送者用户名(发送时刻的 snapshot)。</summary>
    public string SenderUsername { get; private set; } = string.Empty;

    /// <summary>消息内容(已 trim,长度 1–500)。</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>所属聊天频道。</summary>
    public ChatChannel Channel { get; private set; }

    /// <summary>发送时间(UTC)。</summary>
    public DateTime SentAt { get; private set; }

    // EF 物化用。
    private ChatMessage() { }

    internal ChatMessage(
        RoomId roomId,
        UserId senderUserId,
        string senderUsername,
        string content,
        ChatChannel channel,
        DateTime sentAt)
    {
        Id = Guid.NewGuid();
        RoomId = roomId;
        SenderUserId = senderUserId;
        SenderUsername = senderUsername;
        Content = content;
        Channel = channel;
        SentAt = sentAt;
    }
}
