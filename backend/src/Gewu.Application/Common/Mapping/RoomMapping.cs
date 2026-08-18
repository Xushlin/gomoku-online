using Gewu.Application.Common.DTOs;
using Gewu.Domain.Enums;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;

namespace Gewu.Application.Common.Mapping;

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
            GameKey: room.GameKey,
            Status: room.Status,
            Host: UserSummary(room.HostUserId, usernames),
            Black: UserSummary(room.BlackPlayerId, usernames),
            White: room.WhitePlayerId is null ? null : UserSummary(room.WhitePlayerId.Value, usernames),
            SpectatorCount: room.Spectators.Count,
            CreatedAt: room.CreatedAt);
    }

    /// <summary>
    /// 转换为完整状态(含所有 Moves / ChatMessages / Spectators)。
    /// <paramref name="turnTimeoutSeconds"/> 从 <c>GameOptions</c> 注入,嵌入到
    /// <see cref="GameSnapshotDto.TurnTimeoutSeconds"/> 以便客户端倒计时。
    /// </summary>
    public static RoomStateDto ToState(
        this Room room,
        IReadOnlyDictionary<Guid, string> usernames,
        int turnTimeoutSeconds,
        RoomView view)
    {
        var specDtos = room.Spectators
            .Select(id => UserSummary(id, usernames))
            .ToList()
            .AsReadOnly();

        // 围观频道**只给围观者**。这里是那条规则在读取侧唯一的实现处。
        //
        // 它此前不存在:`ToState` 原样返回全部消息,于是任何玩家调 GET /api/rooms/{id}
        // 或收一次 RoomState 广播,就拿到了对手围观区的全部内容。屏幕上看不出来 ——
        // ChatPanel 用 @if (isSpectator()) 藏掉了那个 Tab,而数据早就在客户端里。
        //
        // `view` 是**必需参数**,不是可选的:每个调用点都必须说出这份快照给谁看。
        // 一个可选参数会让"忘了传"和"故意给全部"长得一样。
        var chatDtos = room.ChatMessages
            .Where(m => view.IncludeSpectatorChat || m.Channel != ChatChannel.Spectator)
            .OrderBy(m => m.SentAt)
            .Select(m => new ChatMessageDto(
                m.Id, m.SenderUserId.Value, m.SenderUsername, m.Content, m.Channel, m.SentAt))
            .ToList()
            .AsReadOnly();

        GameSnapshotDto? gameDto = null;
        if (room.Game is not null)
        {
            var orderedMoves = room.Game.Moves.OrderBy(mv => mv.Ply).ToList();
            var moves = orderedMoves
                .Select(mv => new MoveDto(mv.Ply, mv.Row, mv.Col, SeatWire.ToStone(mv.Seat), mv.PlayedAt, mv.FromRow, mv.FromCol, mv.Text))
                .ToList()
                .AsReadOnly();
            var turnStartedAt = orderedMoves.LastOrDefault()?.PlayedAt ?? room.Game.StartedAt;
            gameDto = new GameSnapshotDto(
                Id: room.Game.Id,
                CurrentTurn: SeatWire.ToStone(room.Game.CurrentTurn),
                StartedAt: room.Game.StartedAt,
                EndedAt: room.Game.EndedAt,
                Result: room.Game.Result,
                WinnerUserId: room.Game.WinnerUserId?.Value,
                EndReason: room.Game.EndReason,
                TurnStartedAt: turnStartedAt,
                TurnTimeoutSeconds: turnTimeoutSeconds,
                Moves: moves);
        }

        return new RoomStateDto(
            Id: room.Id.Value,
            Name: room.Name,
            GameKey: room.GameKey,
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
