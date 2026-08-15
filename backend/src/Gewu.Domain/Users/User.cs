namespace Gewu.Domain.Users;

/// <summary>
/// 用户聚合根。承载身份(<see cref="Email"/> / <see cref="Username"/>)、凭据哈希、
/// 启用状态与注册时间,以及一个受控的 <see cref="RefreshTokens"/> 集合。
/// 外部 MUST NOT 直接修改字段;所有变更仅通过领域方法进行。
/// <para>
/// **战绩与 Rating 不在本聚合上。** 它们随棋种分开,住在 <see cref="UserGameStats"/>,
/// 主键 <c>(UserId, GameKey)</c>。本聚合 MUST NOT 保留它们的镜像 —— 既不留"主棋种的分",
/// 也不留跨棋种聚合值。镜像是第二份真源,与 <see cref="UserGameStats"/> 漂移之后的症状是
/// **排行榜与资料页显示不同的分**,而没有任何东西会拦住它。
/// </para>
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
    /// EF 以 <c>IsConcurrencyToken</c> 使用。现在仅由 <see cref="ChangePassword"/> 推进 ——
    /// 战绩写入推的是 <see cref="UserGameStats.RowVersion"/>,那是另一行。
    /// <para>
    /// 令牌分成两个的收益很具体:一个玩家一边下棋一边改密码,此前会撞 409;现在两者写的是
    /// 不同的行,各自的令牌互不干涉。refresh token 路径依然不推 —— 它只操作子集合、不改 User
    /// 父行,并发登录 / 登出本身无冲突,加保护反而把登录流程不必要地串行化。
    /// </para>
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
    /// 创建一个新用户。初始状态:<c>IsActive=true</c>、<c>IsBot=false</c>、
    /// <c>CreatedAt</c> 由调用方通过 <c>IDateTimeProvider</c> 提供。
    /// <para>
    /// 注册 MUST NOT 创建任何 <see cref="UserGameStats"/> 行 —— 一个新用户在**每个**棋种上都
    /// 还没下过,而"没有行"正是那个意思。行在他下完某棋种第一局时才出现。
    /// </para>
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
            IsActive = true,
            IsBot = false,
            CreatedAt = createdAt,
        };
    }

    /// <summary>
    /// 创建一个机器人账号(ai-opponent 能力)。字段:<c>PasswordHash=</c><see cref="BotPasswordHashMarker"/>、
    /// <c>IsActive=true</c>、<c>IsBot=true</c>、<c>CreatedAt=</c><paramref name="createdAt"/>。
    /// 不接受 <c>passwordHash</c> 参数 —— bot 永远不可登录。
    /// 调用方 MUST NOT 在 bot 账号上调用 <see cref="IssueRefreshToken"/>。
    /// <para>
    /// 与 <see cref="Register"/> 同理,不创建 <see cref="UserGameStats"/> 行。bot 的战绩行在它
    /// 下完该棋种第一局时出现 —— bot 对局是计分的(ai-opponent 的反套利约束)。
    /// </para>
    /// </summary>
    public static User RegisterBot(UserId id, Email email, Username username, DateTime createdAt)
    {
        return new User
        {
            Id = id,
            Email = email,
            Username = username,
            PasswordHash = BotPasswordHashMarker,
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

    // RecordGameResult 已搬到 UserGameStats —— 战绩按棋种分行,写入的对象是那一行而不是本聚合。

    /// <summary>
    /// 替换用户密码哈希。调用方(handler)MUST 先验证当前密码、自己调 <c>IPasswordHasher.Hash</c>
    /// 产生 hash,再调本方法 —— Domain 不管密码字符串复杂度,只保障不变量。
    /// <para>
    /// Bot 账号禁止改密(防御):<c>IsBot == true</c> 抛 <see cref="InvalidOperationException"/>。
    /// </para>
    /// <para>
    /// <c>PasswordHash</c> 是 User 父行业务属性,方法末尾调 <c>TouchRowVersion()</c>,
    /// 让并发改密被 EF 乐观并发捕获(与 <see cref="UserGameStats.RecordGameResult"/> 对它那一行
    /// 的纪律相同)。这现在是本聚合上**唯一**推进令牌的路径。
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="newPasswordHash"/> 为 null / 空 / 空白。</exception>
    /// <exception cref="InvalidOperationException"><see cref="IsBot"/> 为 true。</exception>
    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new ArgumentException("Password hash must be non-empty.", nameof(newPasswordHash));
        }
        if (IsBot)
        {
            throw new InvalidOperationException("Bot accounts cannot change password.");
        }

        PasswordHash = newPasswordHash;
        TouchRowVersion();
    }
}
