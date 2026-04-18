namespace Gomoku.Domain.Users;

/// <summary>
/// <see cref="User"/> 聚合内的子实体,承载一枚 refresh token 的**哈希**(永远不存原文)、
/// 签发时间、过期时间与可能的吊销时间戳。由 <see cref="User"/> 通过领域方法管理生命周期,
/// 外部不应直接构造。
/// </summary>
public sealed class RefreshToken
{
    /// <summary>子实体主键。</summary>
    public Guid Id { get; private set; }

    /// <summary>所属用户。</summary>
    public UserId UserId { get; private set; }

    /// <summary>token 原文的 SHA-256 哈希(hex 或 base64 由 Infrastructure 决定,保持与入库一致)。</summary>
    public string TokenHash { get; private set; } = string.Empty;

    /// <summary>过期时间戳(UTC)。</summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>签发时间戳(UTC)。</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>吊销时间戳(UTC);尚未吊销时为 <c>null</c>。</summary>
    public DateTime? RevokedAt { get; private set; }

    // EF Core 物化用;外部不可调用。
    private RefreshToken()
    {
    }

    internal RefreshToken(UserId userId, string tokenHash, DateTime expiresAt, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        RevokedAt = null;
    }

    /// <summary>当且仅当未吊销且未过期时返回 <c>true</c>。</summary>
    /// <param name="now">"现在"的 UTC 时间,由调用方通过 <c>IDateTimeProvider</c> 提供。</param>
    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;

    /// <summary>
    /// 幂等地吊销本 token:若尚未吊销,将 <see cref="RevokedAt"/> 设为 <paramref name="revokedAt"/>;
    /// 若已吊销,保持原时间戳不变。由 <see cref="User"/> 内部调用。
    /// </summary>
    internal void Revoke(DateTime revokedAt)
    {
        if (RevokedAt is null)
        {
            RevokedAt = revokedAt;
        }
    }
}
