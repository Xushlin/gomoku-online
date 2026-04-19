using Gomoku.Application.Common.DTOs;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;

namespace Gomoku.Application.Common.Mapping;

/// <summary>
/// <see cref="Room"/> 聚合到对外 DTO 的转换。Host / 玩家 / 围观者的 Username 由调用方
/// 预先准备一个 <c>Guid → username</c> 字典传入,避免在 mapping 里再查 DB。
/// 聊天消息直接使用 <see cref="ChatMessage.SenderUsername"/> snapshot(不再查 DB)。
/// </summary>
public static class RoomMapping
{
    /// <summary>转换为列表摘要(不含 Moves / ChatMessages / 完整 Spectators)。</summary>
    public static RoomSummaryDto ToSummary(this Room room, IReadOnlyDictionary<Guid, string> usernames)
    {
        return new RoomSummaryDto(
            Id: room.Id.Value,
            Name: room.Name,
            Status: room.Status,
            Host: UserSummary(room.HostUserId, usernames),
            Black: UserSummary(room.BlackPlayerId, usernames),
            White: room.WhitePlayerId is null ? null : UserSummary(room.WhitePlayerId.Value, usernames),
            SpectatorCount: room.Spectators.Count,
            CreatedAt: room.CreatedAt);
    }

    /// <summary>转换为完整状态(含所有 Moves / ChatMessages / Spectators)。</summary>
    public static RoomStateDto ToState(this Room room, IReadOnlyDictionary<Guid, string> usernames)
    {
        var specDtos = room.Spectators
            .Select(id => UserSummary(id, usernames))
            .ToList()
            .AsReadOnly();

        var chatDtos = room.ChatMessages
            .OrderBy(m => m.SentAt)
            .Select(m => new ChatMessageDto(
                m.Id, m.SenderUserId.Value, m.SenderUsername, m.Content, m.Channel, m.SentAt))
            .ToList()
            .AsReadOnly();

        GameSnapshotDto? gameDto = null;
        if (room.Game is not null)
        {
            var moves = room.Game.Moves
                .OrderBy(mv => mv.Ply)
                .Select(mv => new MoveDto(mv.Ply, mv.Row, mv.Col, mv.Stone, mv.PlayedAt))
                .ToList()
                .AsReadOnly();
            gameDto = new GameSnapshotDto(
                Id: room.Game.Id,
                CurrentTurn: room.Game.CurrentTurn,
                StartedAt: room.Game.StartedAt,
                EndedAt: room.Game.EndedAt,
                Result: room.Game.Result,
                WinnerUserId: room.Game.WinnerUserId?.Value,
                Moves: moves);
        }

        return new RoomStateDto(
            Id: room.Id.Value,
            Name: room.Name,
            Status: room.Status,
            Host: UserSummary(room.HostUserId, usernames),
            Black: UserSummary(room.BlackPlayerId, usernames),
            White: room.WhitePlayerId is null ? null : UserSummary(room.WhitePlayerId.Value, usernames),
            Spectators: specDtos,
            Game: gameDto,
            ChatMessages: chatDtos,
            CreatedAt: room.CreatedAt);
    }

    /// <summary>把一组 <see cref="UserId"/> 归集为 Guid 列表,便于 handler 一次性 query。</summary>
    public static IReadOnlyList<Guid> CollectUserIds(this Room room)
    {
        var ids = new HashSet<Guid>
        {
            room.HostUserId.Value,
            room.BlackPlayerId.Value,
        };
        if (room.WhitePlayerId is not null)
        {
            ids.Add(room.WhitePlayerId.Value.Value);
        }
        foreach (var s in room.Spectators)
        {
            ids.Add(s.Value);
        }
        return ids.ToList();
    }

    private static UserSummaryDto UserSummary(UserId id, IReadOnlyDictionary<Guid, string> usernames)
    {
        var name = usernames.TryGetValue(id.Value, out var n) ? n : "<unknown>";
        return new UserSummaryDto(id.Value, name);
    }
}
