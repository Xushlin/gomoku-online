using Gomoku.Domain.Enums;
using Gomoku.Domain.Rooms;

namespace Gomoku.Application.Common.DTOs;

/// <summary>用户在 Room 相关 DTO 里的精简表示(避免暴露 email / 战绩等无关字段)。</summary>
public sealed record UserSummaryDto(Guid Id, string Username);

/// <summary>对局中一步棋的网络表示。</summary>
public sealed record MoveDto(int Ply, int Row, int Col, Stone Stone, DateTime PlayedAt);

/// <summary>
/// 对局结束事件的 payload。<paramref name="EndReason"/> 明示"怎么结束的"(Connected5 / Resigned / TurnTimeout),
/// 客户端据此在 UI 区分"连五胜""对方认输""超时判负"。
/// </summary>
public sealed record GameEndedDto(
    GameResult Result,
    Guid? WinnerUserId,
    DateTime EndedAt,
    GameEndReason EndReason);

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

/// <summary>
/// 对局运行时的完整快照(含全部 Moves,最多 225 条)。
/// <para>
/// <paramref name="TurnStartedAt"/> = 最后一步 <c>PlayedAt</c>,无 Moves 时 = <paramref name="StartedAt"/>;
/// 客户端根据 <c>TurnStartedAt + TurnTimeoutSeconds</c> 本地 tick 倒计时 UI。
/// </para>
/// <para>
/// <paramref name="EndReason"/> 与 <paramref name="Result"/> 同时为 <c>null</c> 或同时非 <c>null</c>。
/// </para>
/// </summary>
public sealed record GameSnapshotDto(
    Guid Id,
    Stone CurrentTurn,
    DateTime StartedAt,
    DateTime? EndedAt,
    GameResult? Result,
    Guid? WinnerUserId,
    GameEndReason? EndReason,
    DateTime TurnStartedAt,
    int TurnTimeoutSeconds,
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
