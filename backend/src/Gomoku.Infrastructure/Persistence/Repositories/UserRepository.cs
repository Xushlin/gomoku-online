using Gomoku.Application.Abstractions;
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
}
