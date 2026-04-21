using Gomoku.Application.Abstractions;
using Gomoku.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace Gomoku.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core 支持的 <see cref="IRoomRepository"/> 实现。
/// <see cref="FindByIdAsync"/> 用 <c>Include</c> 一次加载 Game / Moves / Spectators / ChatMessages
/// 以便 handler 在聚合内完整操作。
/// </summary>
public sealed class RoomRepository : IRoomRepository
{
    private readonly GomokuDbContext _db;

    /// <inheritdoc />
    public RoomRepository(GomokuDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<Room?> FindByIdAsync(RoomId id, CancellationToken cancellationToken) =>
        _db.Rooms
            .Include(r => r.Game!)
                .ThenInclude(g => g.Moves)
            .Include("_spectators")
            .Include("_chatMessages")
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Room>> GetActiveRoomsAsync(CancellationToken cancellationToken)
    {
        var rooms = await _db.Rooms
            .Include(r => r.Game!)
                .ThenInclude(g => g.Moves)
            .Include("_spectators")
            .Where(r => r.Status != RoomStatus.Finished)
            .ToListAsync(cancellationToken);
        return rooms;
    }

    /// <inheritdoc />
    public Task AddAsync(Room room, CancellationToken cancellationToken) =>
        _db.Rooms.AddAsync(room, cancellationToken).AsTask();

    /// <inheritdoc />
    public Task DeleteAsync(Room room, CancellationToken cancellationToken)
    {
        // Game / Moves / Spectators / ChatMessages 都在 EF 配置里设了 OnDelete(Cascade),
        // 一次 Remove 聚合根即可让 SaveChangesAsync 时全部随之消失。
        _db.Rooms.Remove(room);
        _ = cancellationToken; // 同步 API,显式忽略
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoomId>> GetRoomsWithExpiredTurnsAsync(
        DateTime now, int turnTimeoutSeconds, CancellationToken cancellationToken)
    {
        var cutoff = now.AddSeconds(-turnTimeoutSeconds);
        // EF 需要一个可翻译的表达式。策略:先在 SQL 里按 Status=Playing + Game!=null 过滤,
        // 再在内存里对 Moves 历史做 max(PlayedAt) vs StartedAt 比较。Moves 对活跃对局每房平均 < 50,
        // 全量加载的代价远小于"复杂 LINQ GroupBy 不能翻译"的后果。
        // 若后续量级变大(上千活跃对局),此处改写为带 correlated subquery 的 raw SQL。
        var playing = await _db.Rooms
            .Include(r => r.Game!)
                .ThenInclude(g => g.Moves)
            .Where(r => r.Status == RoomStatus.Playing && r.Game != null)
            .ToListAsync(cancellationToken);

        return playing
            .Where(r =>
            {
                var game = r.Game!;
                var lastActivity = game.Moves
                    .OrderByDescending(m => m.Ply)
                    .FirstOrDefault()?.PlayedAt ?? game.StartedAt;
                return lastActivity <= cutoff;
            })
            .Select(r => r.Id)
            .ToList();
    }
}
