using Gomoku.Domain.Users;

namespace Gomoku.Domain.Rooms;

/// <summary>
/// <see cref="Room"/> 聚合内的围观者记录子实体。EF 把它映射为独立联结表 <c>RoomSpectators</c>,
/// 支持 <see cref="UserId"/> 值对象存储与未来扩展(例如 JoinedAt 用于观众留存分析)。
/// <see cref="Room.Spectators"/> 对外投影为 <see cref="UserId"/> 集合,隐藏此实体的存在。
/// 外部不可构造。
/// </summary>
public sealed class RoomSpectator
{
    /// <summary>记录主键。</summary>
    public Guid Id { get; private set; }

    /// <summary>所属房间。</summary>
    public RoomId RoomId { get; private set; }

    /// <summary>围观者用户 Id。</summary>
    public UserId UserId { get; private set; }

    /// <summary>加入围观的时间戳(UTC)。</summary>
    public DateTime JoinedAt { get; private set; }

    // EF 物化用。
    private RoomSpectator() { }

    internal RoomSpectator(RoomId roomId, UserId userId, DateTime joinedAt)
    {
        Id = Guid.NewGuid();
        RoomId = roomId;
        UserId = userId;
        JoinedAt = joinedAt;
    }
}
