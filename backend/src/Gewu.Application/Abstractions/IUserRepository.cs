using Gomoku.Domain.Ai;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;

namespace Gomoku.Application.Abstractions;

/// <summary>
/// 用户聚合的持久化契约。签名只接受 / 返回领域类型,MUST NOT 暴露
/// <c>IQueryable</c>、<c>Expression</c>、EF Core 实体等基础设施细节。
/// 所有"按 refresh token 查找"的场景都返回聚合根 <see cref="User"/>,以遵守"通过聚合根修改"的约束。
/// </summary>
public interface IUserRepository
{
    /// <summary>按主键查找用户;找不到返回 <c>null</c>。</summary>
    Task<User?> FindByIdAsync(UserId id, CancellationToken cancellationToken);

    /// <summary>按邮箱查找用户(邮箱已规范化为小写);找不到返回 <c>null</c>。</summary>
    Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken);

    /// <summary>按用户名查找用户(大小写不敏感);找不到返回 <c>null</c>。</summary>
    Task<User?> FindByUsernameAsync(Username username, CancellationToken cancellationToken);

    /// <summary>
    /// 按 refresh token 的 hash 查找所属用户。实现 MUST 同时加载用户的 <c>RefreshTokens</c>
    /// 子集合,以便 handler 能在聚合内操作对应 token。
    /// </summary>
    Task<User?> FindByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>邮箱是否已被占用。</summary>
    Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken);

    /// <summary>用户名是否已被占用(大小写不敏感)。</summary>
    Task<bool> UsernameExistsAsync(Username username, CancellationToken cancellationToken);

    /// <summary>新增一个用户(未提交,需配合 <see cref="IUnitOfWork.SaveChangesAsync"/>)。</summary>
    Task AddAsync(User user, CancellationToken cancellationToken);

    /// <summary>
    /// 分页返回按 `Rating DESC, Wins DESC, GamesPlayed ASC` 排序的**真人**用户
    /// (<c>IsBot == false</c>)+ 真人总数 Total。bot 账号跟随 ELO 正常更新,
    /// 但 MUST NOT 出现在排行榜。
    /// <para>
    /// 先做一次 <c>CountAsync</c> 得 Total(过滤 bot 后),再
    /// <c>Skip((page-1)*pageSize).Take(pageSize)</c> 物化 Users。
    /// </para>
    /// 返回类型是领域类型,不泄漏 <c>IQueryable</c> / `IOrderedEnumerable` 等 EF 细节。
    /// </summary>
    Task<(IReadOnlyList<User> Users, int Total)> GetLeaderboardPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    /// <summary>
    /// 按难度查找系统 seed 的机器人账号;若对应记录不存在或 <c>IsBot == false</c>,返回 <c>null</c>。
    /// 实现按 <see cref="BotAccountIds.For"/> 的固定主键检索。
    /// </summary>
    Task<User?> FindBotByDifficultyAsync(BotDifficulty difficulty, CancellationToken cancellationToken);

    /// <summary>
    /// 返回所有满足"<c>Status == Playing</c> 且当前回合的玩家 <c>IsBot == true</c>"的房间 Id。
    /// 由 <c>AiMoveWorker</c> 轮询后台使用 —— worker 再按 Id 加载完整聚合。**只返回 Id**,不物化房间
    /// 聚合,以降低轮询开销。
    /// </summary>
    Task<IReadOnlyList<RoomId>> GetRoomsNeedingBotMoveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 按 Username 前缀(大小写不敏感)分页搜索**真人**用户。bot 永远不在结果。
    /// 实现 MUST:过滤 <c>!IsBot</c>;若 <paramref name="prefix"/> 非空按 StartsWith(case-insensitive);
    /// 按 Username ASC 排序;先 <c>CountAsync</c> 得 Total,再 <c>Skip/Take</c>。
    /// </summary>
    Task<(IReadOnlyList<User> Users, int Total)> SearchByUsernamePagedAsync(
        string? prefix,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
