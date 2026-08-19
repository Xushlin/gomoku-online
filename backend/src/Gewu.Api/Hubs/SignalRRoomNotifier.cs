using Gewu.Application.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Mapping;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Microsoft.AspNetCore.SignalR;

namespace Gewu.Api.Hubs;

/// <summary>
/// <see cref="IRoomNotifier"/> 的 SignalR 实现。按 design D7 / D15 的 group 命名规则推送,
/// 围观频道消息只发给 spectators 子群,催促事件只发给被催的那一方用户。
/// </summary>
public sealed class SignalRRoomNotifier : IRoomNotifier
{
    private readonly IHubContext<MatchHub> _hub;
    private readonly IGameRulesRegistry _rules;

    /// <inheritdoc />
    public SignalRRoomNotifier(IHubContext<MatchHub> hub, IGameRulesRegistry rules)
    {
        _hub = hub;
        _rules = rules;
    }

    /// <summary>
    /// 推完整房间状态 —— **每个座位一份,外加观察者一份、围观者一份**。
    /// <para>
    /// 此前是两份:非围观者一份、围观者一份。斗地主的手牌只有一个座位能看,所以"非围观者"
    /// 不能再共用一份 —— 而一旦座位群出现,坐着的人就 MUST NOT 再留在观察者群里,否则他会
    /// 收到两份快照(一份带手牌、一份不带),**看到哪一份由到达顺序决定**。
    /// </para>
    /// <para>
    /// 目标群 MUST **互斥且穷尽**:<c>JoinRoom</c> 按聚合身份把每个连接放进「某个座位」/
    /// 「围观者」/「观察者」之一,所以每个连接恰好收到一份。互斥不成立会让某个连接收到两份;
    /// 不穷尽会让某个连接一份都收不到 —— 后者是 <c>fix-spectator-chat-leak</c> 第一版按"玩家"
    /// 分组时真的发生的事。
    /// </para>
    /// <para>
    /// <b>投影次数从 2 变成 SeatCount + 2,而没有为"没有隐藏信息的棋种"开一条快路。</b>
    /// 那会是两条代码路径,而这套 <c>RoomView</c> 机制存在的全部理由就是不给任何 handler
    /// 一次忘记裁剪的机会。
    /// </para>
    /// </summary>
    /// <param name="room">房间聚合。</param>
    /// <param name="usernames">用户名字典。</param>
    /// <param name="turnTimeoutSeconds">回合超时秒数。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task RoomStateChangedAsync(
        Room room,
        IReadOnlyDictionary<Guid, string> usernames,
        int turnTimeoutSeconds,
        CancellationToken ct)
    {
        // 棋种规则用来算每个座位的私有切片。键解析不出来时是 null —— 那样每个座位拿到的
        // 投影内容相同,与本变更之前一致。
        var rules = _rules.For(room.GameKey);

        // **每个座位一份。** 斗地主的手牌只有一个座位能看,所以"非围观者"不能再共用一份快照。
        // 没有隐藏信息的棋种,这几份内容完全相同 —— 而这里**没有为它开一条快路**:
        // 两条代码路径就是给每个未来的 handler 一次忘记裁剪的机会,而这一整个 RoomView 机制
        // 存在的理由就是不给那种机会。代价是同一份 payload 多发几次,进程内扇出。
        foreach (var seat in room.Seats)
        {
            var forSeat = room.ToState(
                usernames, turnTimeoutSeconds, RoomView.ForSeat(room, seat.Index, rules));
            await _hub.Clients.Group(MatchHub.SeatGroupName(room.Id, seat.Index))
                .SendAsync("RoomState", forSeat, ct);
        }

        // 进了房间、没坐座位、也没围观的那些连接。
        var forObservers = room.ToState(usernames, turnTimeoutSeconds, RoomView.ForObservers(room, rules));
        await _hub.Clients.Group(MatchHub.ObserversGroupName(room.Id))
            .SendAsync("RoomState", forObservers, ct);

        var forSpectators = room.ToState(usernames, turnTimeoutSeconds, RoomView.ForSpectators(room, rules));
        await _hub.Clients.Group(MatchHub.SpectatorsGroupName(room.Id))
            .SendAsync("RoomState", forSpectators, ct);
    }

    /// <inheritdoc />
    public Task PlayerJoinedAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("PlayerJoined", user, ct);

    /// <inheritdoc />
    public Task PlayerLeftAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("PlayerLeft", user, ct);

    /// <inheritdoc />
    public Task SpectatorJoinedAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("SpectatorJoined", user, ct);

    /// <inheritdoc />
    public Task SpectatorLeftAsync(RoomId roomId, UserSummaryDto user, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("SpectatorLeft", user, ct);

    /// <inheritdoc />
    public Task MoveMadeAsync(RoomId roomId, MoveDto move, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("MoveMade", move, ct);

    /// <inheritdoc />
    public Task GameEndedAsync(RoomId roomId, GameEndedDto payload, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId)).SendAsync("GameEnded", payload, ct);

    /// <inheritdoc />
    public Task ChatMessagePostedAsync(RoomId roomId, ChatChannel channel, ChatMessageDto message, CancellationToken ct)
    {
        var group = channel == ChatChannel.Spectator
            ? MatchHub.SpectatorsGroupName(roomId)
            : MatchHub.RoomGroupName(roomId);
        return _hub.Clients.Group(group).SendAsync("ChatMessage", message, ct);
    }

    /// <inheritdoc />
    public Task OpponentUrgedAsync(RoomId roomId, UserId urgedUser, UrgeDto payload, CancellationToken ct) =>
        _hub.Clients.User(urgedUser.Value.ToString()).SendAsync("UrgeReceived", payload, ct);

    /// <inheritdoc />
    public Task RoomDissolvedAsync(RoomId roomId, CancellationToken ct) =>
        _hub.Clients.Group(MatchHub.RoomGroupName(roomId))
            .SendAsync("RoomDissolved", new { RoomId = roomId.Value }, ct);
}
