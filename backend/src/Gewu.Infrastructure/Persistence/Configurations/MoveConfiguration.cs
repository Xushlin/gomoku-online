using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Move = Gewu.Domain.Rooms.Move;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Move"/>(对局内一步棋)的 EF 映射。<c>(GameId, Ply)</c> 唯一。
/// 一步棋是 <c>(FromRow, FromCol) -> (Row, Col)</c>,起点可空。
/// </summary>
public sealed class MoveConfiguration : IEntityTypeConfiguration<Move>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Move> builder)
    {
        builder.ToTable("Moves");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.GameId).IsRequired();
        builder.Property(m => m.Ply).IsRequired();
        // 起点可空:落子类棋种(五子棋 / 一字棋)没有起点,走子类(中国象棋)有。
        // 可空让迁移是纯增量 —— 既有的落子记录不用回填,Down 只丢列。
        builder.Property(m => m.FromRow);
        builder.Property(m => m.FromCol);
        builder.Property(m => m.Row).IsRequired();
        builder.Property(m => m.Col).IsRequired();
        builder.Property(m => m.Stone).HasConversion<int>().IsRequired();
        builder.Property(m => m.PlayedAt).IsRequired();

        builder.HasIndex(m => new { m.GameId, m.Ply }).IsUnique();
    }
}
