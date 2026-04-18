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
}
