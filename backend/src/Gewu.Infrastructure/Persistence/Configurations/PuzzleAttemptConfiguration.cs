using Gewu.Domain.Puzzles;
using Gewu.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary><see cref="PuzzleAttempt"/> 的 EF 映射。</summary>
public sealed class PuzzleAttemptConfiguration : IEntityTypeConfiguration<PuzzleAttempt>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PuzzleAttempt> builder)
    {
        builder.ToTable("PuzzleAttempts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.UserId).HasConversion<UserIdConverter>().IsRequired();
        builder.Property(a => a.PuzzleLevelId).IsRequired();
        builder.Property(a => a.StartedAt).IsRequired();
        builder.Property(a => a.FinishedAt);
        builder.Property(a => a.HintsUsed).IsRequired();
        builder.Property(a => a.Mistakes).IsRequired();
        builder.Property(a => a.Stars);

        builder.Property(a => a.RowVersion).IsConcurrencyToken().IsRequired();

        // 查询模式:按 (id, 所有者) 取尝试 —— 所有权是查询条件的一部分,不是取回后再判。
        builder.HasIndex(a => new { a.Id, a.UserId });
        builder.HasIndex(a => new { a.UserId, a.PuzzleLevelId });

        builder.HasOne<PuzzleLevel>()
            .WithMany()
            .HasForeignKey(a => a.PuzzleLevelId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(a => a.IsCompleted);
        builder.Ignore(a => a.Duration);
    }
}
