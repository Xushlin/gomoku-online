using Gewu.Domain.Puzzles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="PuzzleLevel"/> 的 EF 映射。<c>(GameKey, LevelIndex)</c> 唯一
/// —— 序号既是标识也是解锁顺序,重复会让"前一关"含义不明。
/// </summary>
public sealed class PuzzleLevelConfiguration : IEntityTypeConfiguration<PuzzleLevel>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PuzzleLevel> builder)
    {
        builder.ToTable("PuzzleLevels");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedOnAdd();

        builder.Property(l => l.GameKey).IsRequired().HasMaxLength(64);
        builder.Property(l => l.LevelIndex).IsRequired();
        builder.Property(l => l.Difficulty).IsRequired();
        builder.Property(l => l.LayoutJson).IsRequired();
        builder.Property(l => l.SolutionJson).IsRequired();

        builder.HasIndex(l => new { l.GameKey, l.LevelIndex }).IsUnique();
    }
}
