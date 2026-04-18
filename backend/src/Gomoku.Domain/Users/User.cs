namespace Gomoku.Domain.Users;

/// <summary>
/// 用户聚合根。承载身份(<see cref="Email"/> / <see cref="Username"/>)、凭据哈希、
/// 战绩字段(<see cref="Rating"/>、<see cref="GamesPlayed"/> 等)、启用状态与注册时间,
/// 以及一个受控的 <see cref="RefreshTokens"/> 集合。外部 MUST NOT 直接修改字段;
/// 所有变更仅通过领域方法进行。
/// </summary>
public sealed class User
{
    private readonly List<RefreshToken> _refreshTokens = new();

    /// <summary>主键。</summary>
    public UserId Id { get; private set; }

    /// <summary>登录邮箱(小写规范化)。</summary>
    public Email Email { get; private set; } = default!;

    /// <summary>展示用用户名(大小写保留,比较不敏感)。</summary>
    public Username Username { get; private set; } = default!;

    /// <summary>Identity <c>PasswordHasher</c> V3 格式的密码哈希。</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>当前 ELO 积分;新用户默认 1200。</summary>
    public int Rating { get; private set; }

    /// <summary>累计对局数。</summary>
    public int GamesPlayed { get; private set; }

    /// <summary>累计胜场。</summary>
    public int Wins { get; private set; }

    /// <summary>累计负场。</summary>
    public int Losses { get; private set; }

    /// <summary>累计平局数。</summary>
    public int Draws { get; private set; }

    /// <summary>是否启用;<c>false</c> 时即使凭据正确也拒绝登录。</summary>
    public bool IsActive { get; private set; }

    /// <summary>注册时间(UTC)。</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>聚合内的 refresh token 集合(只读视图 —— 外部 MUST NOT 修改)。</summary>
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens;

    // EF Core 物化用;外部不可调用。
    private User()
    {
    }

    /// <summary>
    /// 创建一个新用户。初始状态:<c>Rating=1200</c>、战绩字段均为 0、<c>IsActive=true</c>、
    /// <c>CreatedAt</c> 由调用方通过 <c>IDateTimeProvider</c> 提供。
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="passwordHash"/> 为 <c>null</c> 或空白。</exception>
    public static User Register(UserId id, Email email, Username username, string passwordHash, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash must be non-empty.", nameof(passwordHash));
        }

        return new User
        {
            Id = id,
            Email = email,
            Username = username,
            PasswordHash = passwordHash,
            Rating = 1200,
            GamesPlayed = 0,
            Wins = 0,
            Losses = 0,
            Draws = 0,
            IsActive = true,
            CreatedAt = createdAt,
        };
    }

    /// <summary>在聚合内发放一枚 refresh token(存哈希,不存原文)。</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="tokenHash"/> 为空;或 <paramref name="expiresAt"/> ≤ <paramref name="issuedAt"/>。
    /// </exception>
    public void IssueRefreshToken(string tokenHash, DateTime expiresAt, DateTime issuedAt)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Refresh token hash must be non-empty.", nameof(tokenHash));
        }

        if (expiresAt <= issuedAt)
        {
            throw new ArgumentException(
                $"expiresAt ({expiresAt:o}) must be greater than issuedAt ({issuedAt:o}).",
                nameof(expiresAt));
        }

        _refreshTokens.Add(new RefreshToken(Id, tokenHash, expiresAt, issuedAt));
    }

    /// <summary>按 hash 吊销一枚 token。找到返回 <c>true</c>;找不到返回 <c>false</c>;已吊销不覆盖。</summary>
    public bool RevokeRefreshToken(string tokenHash, DateTime revokedAt)
    {
        var token = _refreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash);
        if (token is null)
        {
            return false;
        }

        token.Revoke(revokedAt);
        return true;
    }

    /// <summary>吊销当前所有未吊销的 token(已吊销的保持原时间戳)。</summary>
    public void RevokeAllRefreshTokens(DateTime revokedAt)
    {
        foreach (var token in _refreshTokens)
        {
            if (token.RevokedAt is null)
            {
                token.Revoke(revokedAt);
            }
        }
    }
}
