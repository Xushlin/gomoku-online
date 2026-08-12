using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Move = Gomoku.Domain.Rooms.Move;

namespace Gomoku.Infrastructure.Persistence;

/// <summary>
/// 应用主 <see cref="DbContext"/>。Code-first 建模,配置通过
/// <see cref="IEntityTypeConfiguration{TEntity}"/> 分拆到同目录 <c>Configurations/</c> 文件夹。
/// </summary>
public sealed class GomokuDbContext : DbContext
{
    /// <summary>用户聚合根。</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>刷新令牌子实体。</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>房间聚合根。</summary>
    public DbSet<Room> Rooms => Set<Room>();

    /// <summary>对局子实体。</summary>
    public DbSet<Game> Games => Set<Game>();

    /// <summary>落子记录子实体。</summary>
    public DbSet<Move> Moves => Set<Move>();

    /// <summary>房间聊天消息。</summary>
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    /// <summary>房间围观者联结记录。</summary>
    public DbSet<RoomSpectator> RoomSpectators => Set<RoomSpectator>();

    /// <inheritdoc />
    public GomokuDbContext(DbContextOptions<GomokuDbContext> options) : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GomokuDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
