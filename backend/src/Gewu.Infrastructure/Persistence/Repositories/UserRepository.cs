using Gewu.Application.Abstractions;
using Gewu.Domain.Ai;
using Gewu.Domain.Enums;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core 支持的 <see cref="IUserRepository"/> 实现。<c>Email</c> / <c>Username</c>
/// 以 <c>ComplexProperty</c> 映射;LINQ 中 <c>u.Email.Value</c> 会被翻译为对单列的比较。
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    /// <inheritdoc />
    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<User?> FindByIdAsync(UserId id, CancellationToken cancellationToken) =>
        _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken)
    {
        var value = email.Value;
        return _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Email.Value == value, cancellationToken);
    }

    /// <inheritdoc />
    public Task<User?> FindByUsernameAsync(Username username, CancellationToken cancellationToken)
    {
        var value = username.Value;
        return _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Username.Value == value, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> FindByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        // 先按子实体 hash 定位 UserId(物化为实体,避免 EF.Property 对自定义
        // ValueConverter 的类型强转问题),再把该用户连同所有 tokens 一次性加载。
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (token is null)
        {
            return null;
        }

        return await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken)
    {
        var value = email.Value;
        return _db.Users.AnyAsync(u => u.Email.Value == value, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> UsernameExistsAsync(Username username, CancellationToken cancellationToken)
    {
        var value = username.Value;
        // Username 列带 COLLATE NOCASE,直接等值比较即忽略大小写。
        return _db.Users.AnyAsync(u => u.Username.Value == value, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _db.Users.AddAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserGameStats> GetOrCreateGameStatsAsync(
        UserId userId, string gameKey, CancellationToken cancellationToken)
    {
        var existing = await _db.UserGameStats
            .FirstOrDefaultAsync(s => s.UserId == userId && s.GameKey == gameKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = UserGameStats.Start(userId, gameKey);
        await _db.UserGameStats.AddAsync(created, cancellationToken);
        // 刻意不 SaveChanges —— 新行要和对局结束的其它变更合并到同一事务。
        return created;
    }

    /// <inheritdoc />
    public Task<UserGameStats?> FindGameStatsAsync(
        UserId userId, string gameKey, CancellationToken cancellationToken) =>
        _db.UserGameStats
            .FirstOrDefaultAsync(s => s.UserId == userId && s.GameKey == gameKey, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, UserGameStats>> FindGameStatsForAsync(
        IEnumerable<UserId> userIds, string gameKey, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, UserGameStats>();
        }

        var rows = await _db.UserGameStats
            .Where(s => s.GameKey == gameKey && ids.Contains(s.UserId))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(s => s.UserId.Value);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<UserGameStats> Entries, int Total)> GetLeaderboardPagedAsync(
        string gameKey, int page, int pageSize, CancellationToken cancellationToken)
    {
        // GameKey 谓词下推到 EF,不在内存里筛。bot 过滤靠 join 回 Users —— 机器人跟随 ELO 正常
        // 更新(反套利),但不进排行榜(见 elo-rating spec)。
        var baseQuery =
            from s in _db.UserGameStats
            join u in _db.Users on s.UserId equals u.Id
            where s.GameKey == gameKey && !u.IsBot
            select s;

        var total = await baseQuery.CountAsync(cancellationToken);
        var entries = await baseQuery
            .OrderByDescending(s => s.Rating)
            .ThenByDescending(s => s.Wins)
            .ThenBy(s => s.GamesPlayed)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (entries, total);
    }

    /// <inheritdoc />
    public async Task<User?> FindBotByDifficultyAsync(BotDifficulty difficulty, CancellationToken cancellationToken)
    {
        var id = new UserId(BotAccountIds.For(difficulty));
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        // 若记录存在但不是 bot(异常 seed),视为未配置。
        return user is { IsBot: true } ? user : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoomId>> GetRoomsNeedingBotMoveAsync(CancellationToken cancellationToken)
    {
        // "Playing 且当前回合那个座位上坐的是 bot"。
        //
        // **座位化之后这条查询变简单了。** 此前它 JOIN 两次 `Users`(黑方一次、白方一次),
        // 再用两个分支各写一遍同一件事 —— 而那两个分支正是三座位下要加第三个的形状。
        // 现在是一次 JOIN,条件就是 `s.Index == g.CurrentTurn`:座位数不再出现在查询里。
        var query =
            from r in _db.Rooms
            where r.Status == RoomStatus.Playing
            join g in _db.Games on r.Id equals g.RoomId
            join s in _db.RoomSeats on r.Id equals s.RoomId
            join u in _db.Users on s.UserId equals u.Id
            where s.Index == g.CurrentTurn && u.IsBot
            select r.Id;

        var ids = await query.ToListAsync(cancellationToken);
        return ids;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<User> Users, int Total)> SearchByUsernamePagedAsync(
        string? prefix, int page, int pageSize, CancellationToken cancellationToken)
    {
        // 基础查询:过滤 bot,让 search 不出现 AI_Easy 等。
        IQueryable<User> baseQuery = _db.Users.Where(u => !u.IsBot);

        if (!string.IsNullOrEmpty(prefix))
        {
            // Username 列已用 NOCASE collation(见 UserConfiguration),EF 翻译的 SQL LIKE
            // 天然大小写不敏感。显式 ToLower 作为 OrdinalIgnoreCase 行为的兜底。
            var lower = prefix.ToLowerInvariant();
            baseQuery = baseQuery.Where(u => u.Username.Value.ToLower().StartsWith(lower));
        }

        var total = await baseQuery.CountAsync(cancellationToken);

        var users = await baseQuery
            .OrderBy(u => u.Username.Value)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (users, total);
    }
}
