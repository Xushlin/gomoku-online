using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gewu.Infrastructure.Persistence.Configurations;

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
        builder.Property(g => g.CurrentTurn).IsRequired();

        // 服务端侧的对局设置。**刻意不设长度上限**:内核不解释它,所以"多长算长"是那个棋种
        // 的事(斗地主一副牌是 57 字符)。刻意**不**加 `.IsRequired()` —— 四个现有棋种没有设置,
        // 而 `generalize-match-payload` 已经付过一次这个账:显式配置盖过 CLR 可空性,
        // 于是类型改了、迁移干净生成了,而数据库在第一次写 NULL 时才拒绝。
        builder.Property(g => g.Setup);

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
