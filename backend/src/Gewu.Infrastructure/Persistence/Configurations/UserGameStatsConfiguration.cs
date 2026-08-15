using Gewu.Domain.Users;
using Gewu.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="UserGameStats"/> 的 EF 映射:复合主键 <c>(UserId, GameKey)</c>,
/// 外键指向 <see cref="User"/> 且随用户级联删除。
/// </summary>
public sealed class UserGameStatsConfiguration : IEntityTypeConfiguration<UserGameStats>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserGameStats> builder)
    {
        builder.ToTable("UserGameStats");

        builder.HasKey(s => new { s.UserId, s.GameKey });

        builder.Property(s => s.UserId)
            .HasConversion<UserIdConverter>()
            .IsRequired();

        builder.Property(s => s.GameKey)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(s => s.Rating).IsRequired();
        builder.Property(s => s.GamesPlayed).IsRequired();
        builder.Property(s => s.Wins).IsRequired();
        builder.Property(s => s.Losses).IsRequired();
        builder.Property(s => s.Draws).IsRequired();

        // 与 User.RowVersion 同一机制,但保护的是**本行**。分开的收益很具体:一个玩家一边下棋
        // 一边改密码此前会撞 409;同一玩家两个不同棋种的对局同时结束也会互撞。现在写的是不同行。
        builder.Property(s => s.RowVersion)
            .IsConcurrencyToken()
            .IsRequired();

        // 排行榜的查询形状:先按棋种过滤,再按 Rating 降序取一页。把两者放进同一个索引,
        // 让分页扫描不必回表排序。Wins / GamesPlayed 是二三级排序,数据量到之前不值得进索引。
        builder.HasIndex(s => new { s.GameKey, s.Rating })
            .HasDatabaseName("IX_UserGameStats_GameKey_Rating");

        // 用户被删时它的战绩一并消失 —— 一行战绩脱离用户没有任何意义。
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
