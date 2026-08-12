using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using Gomoku.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gomoku.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Game"/> 子实体映射。<c>RowVersion</c> 启用乐观并发。<c>Moves</c> 由
/// <see cref="MoveConfiguration"/> 独立建表。
/// </summary>
public sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();

        builder.Property(g => g.RoomId).HasConversion<RoomIdConverter>().IsRequired();

        builder.Property(g => g.StartedAt).IsRequired();
        builder.Property(g => g.EndedAt);
        builder.Property(g => g.Result).HasConversion<int?>();
        builder.Property(g => g.EndReason).HasConversion<int?>();
        builder.Property(g => g.WinnerUserId)
            .HasConversion(v => v.HasValue ? v.Value.Value : (Guid?)null,
                           v => v.HasValue ? new UserId(v.Value) : (UserId?)null);
        builder.Property(g => g.CurrentTurn).HasConversion<int>().IsRequired();

        // SQLite 没有原生 rowversion,由 Domain 在每次状态变更后手动更新
        // (见 Game.TouchRowVersion);EF 只把它当作并发令牌检查。
        builder.Property(g => g.RowVersion)
            .IsConcurrencyToken()
            .IsRequired();

        // Moves: 1:N
        builder.HasMany(g => g.Moves)
            .WithOne()
            .HasForeignKey(m => m.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Game.Moves))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
