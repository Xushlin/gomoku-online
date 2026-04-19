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
}
