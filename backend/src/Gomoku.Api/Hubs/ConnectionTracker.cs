using System.Collections.Concurrent;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;

namespace Gomoku.Api.Hubs;

/// <summary>
/// 进程内 <see cref="IConnectionTracker"/> 实现:两个 <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// 各自按 connectionId / roomId 索引。适合单实例部署。
/// </summary>
public sealed class ConnectionTracker : IConnectionTracker
{
    private readonly ConcurrentDictionary<string, ConnectionState> _byConnection = new();
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _byRoom = new();

    /// <inheritdoc />
    public ValueTask TrackAsync(string connectionId, UserId userId)
    {
        _byConnection[connectionId] = new ConnectionState(userId, new HashSet<Guid>());
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

    private sealed record ConnectionState(UserId UserId, HashSet<Guid> Rooms);
}
