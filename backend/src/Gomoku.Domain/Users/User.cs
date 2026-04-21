namespace Gomoku.Domain.Users;

/// <summary>
/// 用户聚合根。承载身份(<see cref="Email"/> / <see cref="Username"/>)、凭据哈希、
/// 战绩字段(<see cref="Rating"/>、<see cref="GamesPlayed"/> 等)、启用状态与注册时间,
/// 以及一个受控的 <see cref="RefreshTokens"/> 集合。外部 MUST NOT 直接修改字段;
/// 所有变更仅通过领域方法进行。
/// </summary>
public sealed class User
{
    /// <summary>
    /// 机器人账号的 <see cref="PasswordHash"/> 占位常量。
    /// 该值不是任何合法 Identity PasswordHasher V3 输出,<c>PasswordHasher.Verify</c> 对其永远返回 <c>Failed</c>,
    /// 因此即便被人误当作密码去比对也无法通过;迁移 seed 与"登录拒绝 bot"的防御检查都以此常量为锚。
    /// </summary>
    public const string BotPasswordHashMarker = "__BOT_NO_LOGIN__";

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

    /// <summary>
    /// 是否为系统机器人账号。真人通过 <see cref="Register"/> 创建时恒为 <c>false</c>;
    /// 机器人通过 <see cref="RegisterBot"/> 创建(并由 migration seed 写入)时为 <c>true</c>。
    /// Bot 账号 MUST NOT 登录(由 Login / Refresh handler 显式拒绝),MUST NOT 出现在排行榜。
    /// </summary>
    public bool IsBot { get; private set; }

    /// <summary>注册时间(UTC)。</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// 乐观并发令牌。SQLite 没有原生 rowversion 列,Domain 自管 16 字节 <see cref="Guid"/> 值,
    /// EF 以 <c>IsConcurrencyToken</c> 使用。仅在 <see cref="RecordGameResult"/> 末尾触发
    /// <see cref="TouchRowVersion"/> 刷新 —— refresh token 路径只操作子集合,不改 User 父行,
    /// 并发场景无冲突,加保护反而把登录流程不必要地串行化。
    /// </summary>
    public byte[] RowVersion { get; private set; } = Guid.NewGuid().ToByteArray();

    /// <summary>聚合内的 refresh token 集合(只读视图 —— 外部 MUST NOT 修改)。</summary>
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens;

    private void TouchRowVersion() => RowVersion = Guid.NewGuid().ToByteArray();

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
            IsBot = false,
            CreatedAt = createdAt,
        };
    }

    /// <summary>
    /// 创建一个机器人账号(ai-opponent 能力)。字段:<c>PasswordHash=</c><see cref="BotPasswordHashMarker"/>、
    /// <c>Rating=1200</c>、战绩计数器均为 0、<c>IsActive=true</c>、<c>IsBot=true</c>、
    /// <c>CreatedAt=</c><paramref name="createdAt"/>。
    /// 不接受 <c>passwordHash</c> 参数 —— bot 永远不可登录。
    /// 调用方 MUST NOT 在 bot 账号上调用 <see cref="IssueRefreshToken"/>。
    /// </summary>
    public static User RegisterBot(UserId id, Email email, Username username, DateTime createdAt)
    {
        return new User
        {
            Id = id,
            Email = email,
            Username = username,
            PasswordHash = BotPasswordHashMarker,
            Rating = 1200,
            GamesPlayed = 0,
            Wins = 0,
            Losses = 0,
            Draws = 0,
            IsActive = true,
            IsBot = true,
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

    /// <summary>
    /// 记录一局对局的结果,原子完成三件事:<c>GamesPlayed++</c>、按 <paramref name="outcome"/>
    /// 递增对应计数器(<see cref="Wins"/> / <see cref="Losses"/> / <see cref="Draws"/>)、
    /// 将 <see cref="Rating"/> 设置为 <paramref name="newRating"/>。
    /// <para>
    /// 这是 elo-rating 能力对 User 聚合的唯一写入口;调用方(handler)MUST 先用
    /// <see cref="EloRating.EloRating.Calculate"/> 算出新积分再调用本方法 —— 本方法不做 ELO 计算,
    /// 也不校验 <paramref name="newRating"/> 的合理性。
    /// </para>
    /// <para>
    /// 不变量:调用后 <c>Wins + Losses + Draws == GamesPlayed</c>。
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outcome"/> 不是 <see cref="GameOutcome"/> 定义值(Loss / Win / Draw)。
    /// 抛出时 User 状态保持不变。
    /// </exception>
    public void RecordGameResult(GameOutcome outcome, int newRating)
    {
        switch (outcome)
        {
            case GameOutcome.Win:
                Wins++;
                break;
            case GameOutcome.Loss:
                Losses++;
                break;
            case GameOutcome.Draw:
                Draws++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown GameOutcome value.");
        }

        GamesPlayed++;
        Rating = newRating;
        TouchRowVersion();
    }
}
