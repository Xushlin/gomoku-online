using System.Collections.Concurrent;
using System.Collections.Generic;
using Gomoku.Application.Abstractions;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;

namespace Gomoku.Api.Hubs;

/// <summary>
/// 进程内 <see cref="IConnectionTracker"/> 实现。
/// <para>
/// 三个并发字典:
/// <list type="bullet">
/// <item><c>_byConnection</c>: connectionId → (UserId + 房间集合),断连时用。</item>
/// <item><c>_byRoom</c>: roomId → 该房间的 connectionId 集合,用于 room 侧回收。</item>
/// <item><c>_onlineCountsByUser</c>: UserId → 活连接数。同用户多连接递增合计;计数归零
///     则 TryRemove。<see cref="GetOnlineUserCount"/> 直接用 <c>Count</c>,
///     <see cref="IsUserOnline"/> 用 <c>TryGetValue + &gt; 0</c>。</item>
/// </list>
/// </para>
/// </summary>
public sealed class ConnectionTracker : IConnectionTracker
{
    private readonly ConcurrentDictionary<string, ConnectionState> _byConnection = new();
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _byRoom = new();
    private readonly ConcurrentDictionary<UserId, int> _onlineCountsByUser = new();

    /// <inheritdoc />
    public ValueTask TrackAsync(string connectionId, UserId userId)
    {
        _byConnection[connectionId] = new ConnectionState(userId, new HashSet<Guid>());
        _onlineCountsByUser.AddOrUpdate(userId, 1, (_, count) => count + 1);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask UntrackAsync(string connectionId)
    {
        if (_byConnection.TryRemove(connectionId, out var state))
        {
            foreach (var roomGuid in state.Rooms)
            {
                if (_byRoom.TryGetValue(roomGuid, out var set))
                {
                    lock (set)
                    {
                        set.Remove(connectionId);
                        if (set.Count == 0)
                        {
                            _byRoom.TryRemove(roomGuid, out _);
                        }
                    }
                }
            }

            DecrementOnlineCount(state.UserId);
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask AssociateRoomAsync(string connectionId, RoomId roomId)
    {
        if (_byConnection.TryGetValue(connectionId, out var state))
        {
            lock (state.Rooms)
            {
                state.Rooms.Add(roomId.Value);
            }
        }
        var set = _byRoom.GetOrAdd(roomId.Value, _ => new HashSet<string>());
        lock (set)
        {
            set.Add(connectionId);
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DissociateRoomAsync(string connectionId, RoomId roomId)
    {
        if (_byConnection.TryGetValue(connectionId, out var state))
        {
            lock (state.Rooms)
            {
                state.Rooms.Remove(roomId.Value);
            }
        }
        if (_byRoom.TryGetValue(roomId.Value, out var set))
        {
            lock (set)
            {
                set.Remove(connectionId);
            }
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public int GetOnlineUserCount() => _onlineCountsByUser.Count;

    /// <inheritdoc />
    public bool IsUserOnline(UserId userId)
        => _onlineCountsByUser.TryGetValue(userId, out var count) && count > 0;

    /// <summary>原子递减:为 0 时移除;用 compare-and-swap 循环避免并发 race。</summary>
    private void DecrementOnlineCount(UserId userId)
    {
        while (_onlineCountsByUser.TryGetValue(userId, out var current))
        {
            var next = current - 1;
            if (next <= 0)
            {
                // key/value pair 精确匹配时才移除,避免误删他人递增过的 key
                if (_onlineCountsByUser.TryRemove(new KeyValuePair<UserId, int>(userId, current)))
                {
                    return;
                }
            }
            else
            {
                if (_onlineCountsByUser.TryUpdate(userId, next, current))
                {
                    return;
                }
            }
            // race:若 TryUpdate / TryRemove 都失败,重读 + 重试
        }
    }

    private sealed record ConnectionState(UserId UserId, HashSet<Guid> Rooms);
}
