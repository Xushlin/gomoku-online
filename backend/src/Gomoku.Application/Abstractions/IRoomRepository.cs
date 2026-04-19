using Gomoku.Domain.Rooms;

namespace Gomoku.Application.Abstractions;

/// <summary>
/// 房间聚合的持久化契约。签名只接受 / 返回领域类型,MUST NOT 暴露
/// <c>IQueryable</c> / <c>Expression</c> / EF Core 实体。
/// 实现 MUST 在 <c>FindByIdAsync</c> 中 Include 完整子实体链(Game / Moves / ChatMessages),
/// 以便 handler 在聚合内操作。
/// </summary>
public interface IRoomRepository
{
    /// <summary>按主键加载房间及其所有子实体;不存在返回 <c>null</c>。</summary>
    Task<Room?> FindByIdAsync(RoomId id, CancellationToken cancellationToken);

    /// <summary>返回所有 Waiting / Playing 状态的房间,不含 Finished。</summary>
    Task<IReadOnlyList<Room>> GetActiveRoomsAsync(CancellationToken cancellationToken);

    /// <summary>新增一个房间(未提交,需 <see cref="IUnitOfWork.SaveChangesAsync"/>)。</summary>
    Task AddAsync(Room room, CancellationToken cancellationToken);
}
