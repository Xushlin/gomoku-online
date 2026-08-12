using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;

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

    /// <summary>
    /// 标记删除一个房间聚合。实现 MUST 仅把实体从 EF 上下文中 <c>Remove</c>,
    /// MUST NOT 调 <c>SaveChangesAsync</c>(交给 handler 的 <see cref="IUnitOfWork"/>);
    /// Game / Moves / Spectators / ChatMessages 由 EF <c>OnDelete(Cascade)</c> 自动随根删除。
    /// </summary>
    Task DeleteAsync(Room room, CancellationToken cancellationToken);

    /// <summary>
    /// 返回所有"当前回合已超时"的房间 Id。匹配规则:
    /// <list type="bullet">
    /// <item><c>Status == Playing</c> 且 <c>Game != null</c></item>
    /// <item><c>max(Moves.PlayedAt, Game.StartedAt) + turnTimeoutSeconds &lt;= now</c></item>
    /// </list>
    /// 只返回 <see cref="RoomId"/>,MUST NOT 物化 <see cref="Room"/> 聚合(worker 再按 Id 加载)。
    /// 由 <c>TurnTimeoutWorker</c> 调用;签名不泄漏 EF 类型。
    /// </summary>
    Task<IReadOnlyList<RoomId>> GetRoomsWithExpiredTurnsAsync(
        DateTime now,
        int turnTimeoutSeconds,
        CancellationToken cancellationToken);

    /// <summary>
    /// 返回指定用户**当前参与**的 Waiting + Playing 房间(不含 Finished、不含围观)。
    /// 实现 MUST:过滤 <c>Status != Finished</c> 且 <c>BlackPlayerId == userId OR WhitePlayerId == userId</c>;
    /// 按 <c>CreatedAt DESC</c> 排序;Include Game + Moves + _spectators(为映射 RoomSummaryDto 准备)。
    /// 典型返回 0-5 条,不分页。
    /// </summary>
    Task<IReadOnlyList<Room>> GetActiveRoomsByUserAsync(
        UserId userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 分页返回指定用户参与过的 Finished 对局房间,按 <c>Game.EndedAt DESC</c> 排序。
    /// 实现 MUST:
    /// <list type="bullet">
    /// <item>过滤 <c>Status == Finished</c> 且 <c>BlackPlayerId == userId OR WhitePlayerId == userId</c></item>
    /// <item>先做一次 <c>CountAsync</c> 得 Total,再 <c>Skip((page-1)*pageSize).Take(pageSize)</c></item>
    /// <item><c>Include Game + Game.Moves</c> 以便调用方算 MoveCount(战绩列表卡片用)</item>
    /// </list>
    /// 签名不暴露 EF 类型。
    /// </summary>
    Task<(IReadOnlyList<Room> Rooms, int Total)> GetUserFinishedGamesPagedAsync(
        UserId userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
