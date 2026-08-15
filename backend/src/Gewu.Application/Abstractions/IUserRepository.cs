using Gewu.Domain.Ai;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;

namespace Gewu.Application.Abstractions;

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
    /// 取或建某玩家在某棋种上的战绩行。不存在时以初始值(<c>Rating = 1200</c>、战绩全 0)新建
    /// 并加入变更跟踪。
    /// <para>
    /// 是 get-or-**create** 而不是 find-or-throw:"第一次下这个棋种"是常态而不是异常。
    /// </para>
    /// <para>
    /// 实现 MUST NOT 自行调 <c>SaveChangesAsync</c> —— 新行要和对局结束的其它变更合并到同一事务。
    /// </para>
    /// <para>
    /// 因为它会**建行**,只该由对局结束路径调用。只读路径(资料页 / 搜索)MUST 用
    /// <see cref="FindGameStatsAsync"/> —— 一次 GET 请求把人凭空登记进某个棋种的排行榜,
    /// 会把"下过"的含义变成"被人看过资料"。
    /// </para>
    /// </summary>
    Task<UserGameStats> GetOrCreateGameStatsAsync(
        UserId userId,
        string gameKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// 只读查询某玩家在某棋种上的战绩行;没有返回 <c>null</c>。**不新建**。
    /// </summary>
    Task<UserGameStats?> FindGameStatsAsync(
        UserId userId,
        string gameKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// 批量只读查询一组玩家在**同一棋种**上的战绩行,返回 "Guid → 战绩" 字典;没有行的 id 不进 dict。
    /// <para>
    /// 供搜索结果这类"一页用户 + 各自的分"的场景使用,避免逐个 <see cref="FindGameStatsAsync"/>
    /// 打出 N 次往返。同样**不新建**。
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, UserGameStats>> FindGameStatsForAsync(
        IEnumerable<UserId> userIds,
        string gameKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// 分页返回某棋种上按 `Rating DESC, Wins DESC, GamesPlayed ASC` 排序的**真人**战绩行
    /// + 真人总数 Total。bot 账号跟随 ELO 正常更新,但 MUST NOT 出现在排行榜。
    /// <para>
    /// 实现 MUST 把 <paramref name="gameKey"/> 谓词**下推到 EF**,不在内存里筛。
    /// 没有该棋种战绩行的用户 MUST NOT 出现在榜上,也 MUST NOT 以初始 1200 分占位 ——
    /// 否则一个从没下过一字棋的人会出现在一字棋榜上,位置取决于有多少人恰好也没下过。
    /// </para>
    /// <para>
    /// 先做一次 <c>CountAsync</c> 得 Total(过滤 bot 后),再
    /// <c>Skip((page-1)*pageSize).Take(pageSize)</c> 物化。
    /// </para>
    /// 返回 <see cref="UserGameStats"/> 而不是 <see cref="User"/>:榜要的是"某人在某棋种上的分",
    /// 那正是这个实体承载的东西;用户名由调用方经 <c>LookupUsernamesAsync</c> 另取。
    /// 返回类型是领域类型,不泄漏 <c>IQueryable</c> / `IOrderedEnumerable` 等 EF 细节。
    /// </summary>
    Task<(IReadOnlyList<UserGameStats> Entries, int Total)> GetLeaderboardPagedAsync(
        string gameKey,
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
