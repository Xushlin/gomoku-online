using Gomoku.Domain.Enums;
using Gomoku.Domain.Rooms;

namespace Gomoku.Application.Common.DTOs;

/// <summary>用户在 Room 相关 DTO 里的精简表示(避免暴露 email / 战绩等无关字段)。</summary>
public sealed record UserSummaryDto(Guid Id, string Username);

/// <summary>对局中一步棋的网络表示。</summary>
public sealed record MoveDto(int Ply, int Row, int Col, Stone Stone, DateTime PlayedAt);

/// <summary>对局结束事件的 payload。</summary>
public sealed record GameEndedDto(GameResult Result, Guid? WinnerUserId, DateTime EndedAt);

/// <summary>
/// 房间摘要,用于 <c>GET /api/rooms</c> 列表。不含 Moves / ChatMessages / Spectators 列表,
/// 只含观众数量。
/// </summary>
public sealed record RoomSummaryDto(
    Guid Id,
    string Name,
    RoomStatus Status,
    UserSummaryDto Host,
    UserSummaryDto? Black,
    UserSummaryDto? White,
    int SpectatorCount,
    DateTime CreatedAt);

/// <summary>对局运行时的完整快照(含全部 Moves,最多 225 条)。</summary>
public sealed record GameSnapshotDto(
    Guid Id,
    Stone CurrentTurn,
    DateTime StartedAt,
    DateTime? EndedAt,
    GameResult? Result,
    Guid? WinnerUserId,
    IReadOnlyList<MoveDto> Moves);

/// <summary>聊天消息的网络表示。</summary>
public sealed record ChatMessageDto(
    Guid Id,
    Guid SenderUserId,
    string SenderUsername,
    string Content,
    ChatChannel Channel,
    DateTime SentAt);

/// <summary>催促事件的 payload(仅推给被催方)。</summary>
public sealed record UrgeDto(Guid FromUserId, string FromUsername, DateTime SentAt);

/// <summary>房间的完整状态,用于 <c>GET /api/rooms/{id}</c> 和 <c>RoomStateChanged</c> 事件。</summary>
public sealed record RoomStateDto(
    Guid Id,
    string Name,
    RoomStatus Status,
    UserSummaryDto Host,
    UserSummaryDto? Black,
    UserSummaryDto? White,
    IReadOnlyList<UserSummaryDto> Spectators,
    GameSnapshotDto? Game,
    IReadOnlyList<ChatMessageDto> ChatMessages,
    DateTime CreatedAt);
