using Gomoku.Application.Abstractions;
using Gomoku.Domain.Ai;
using Gomoku.Domain.Enums;
using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Gomoku.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core 支持的 <see cref="IUserRepository"/> 实现。<c>Email</c> / <c>Username</c>
/// 以 <c>ComplexProperty</c> 映射;LINQ 中 <c>u.Email.Value</c> 会被翻译为对单列的比较。
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly GomokuDbContext _db;

    /// <inheritdoc />
    public UserRepository(GomokuDbContext db)
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
    public async Task<(IReadOnlyList<User> Users, int Total)> GetLeaderboardPagedAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var baseQuery = _db.Users.Where(u => !u.IsBot); // 机器人不进排行榜(见 elo-rating spec)
        var total = await baseQuery.CountAsync(cancellationToken);
        var users = await baseQuery
            .OrderByDescending(u => u.Rating)
            .ThenByDescending(u => u.Wins)
            .ThenBy(u => u.GamesPlayed)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (users, total);
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
        // "Playing 且当前回合玩家是 bot"。CurrentTurn == Black → BlackPlayerId 是 bot;
        // CurrentTurn == White → WhitePlayerId 是 bot。
        // 为让 EF 能翻译,避免 navigation 到 User 的 subquery,先 JOIN User 两次:
        var query =
            from r in _db.Rooms
            where r.Status == RoomStatus.Playing
            join g in _db.Games on r.Id equals g.RoomId
            join blackUser in _db.Users on r.BlackPlayerId equals blackUser.Id
            join whiteUser in _db.Users on r.WhitePlayerId!.Value equals whiteUser.Id
            where (g.CurrentTurn == Stone.Black && blackUser.IsBot)
               || (g.CurrentTurn == Stone.White && whiteUser.IsBot)
            select r.Id;

        var ids = await query.ToListAsync(cancellationToken);
        return ids;
    }
}
