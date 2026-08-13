using Gewu.Domain.Puzzles;
using Gewu.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="PuzzleLevelProgress"/> 的 EF 映射。复合主键 <c>(UserId, PuzzleLevelId)</c>
/// —— 每人每关最多一行,这条约束本身就排除了"同一关两条最好成绩"的状态。
/// </summary>
public sealed class PuzzleLevelProgressConfiguration : IEntityTypeConfiguration<PuzzleLevelProgress>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PuzzleLevelProgress> builder)
    {
        builder.ToTable("PuzzleLevelProgress");

        builder.HasKey(p => new { p.UserId, p.PuzzleLevelId });

        builder.Property(p => p.UserId).HasConversion<UserIdConverter>().IsRequired();
        builder.Property(p => p.PuzzleLevelId).IsRequired();
        builder.Property(p => p.BestStars).IsRequired();
        builder.Property(p => p.BestDurationMs).IsRequired();
        builder.Property(p => p.AttemptCount).IsRequired();

        builder.HasOne<PuzzleLevel>()
            .WithMany()
            .HasForeignKey(p => p.PuzzleLevelId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
