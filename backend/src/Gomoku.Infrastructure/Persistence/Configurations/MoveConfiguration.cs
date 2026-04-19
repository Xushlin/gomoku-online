using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Move = Gomoku.Domain.Rooms.Move;

namespace Gomoku.Infrastructure.Persistence.Configurations;

/// <summary><see cref="Move"/>(对局内落子)的 EF 映射。<c>(GameId, Ply)</c> 唯一。</summary>
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
        builder.Property(m => m.Row).IsRequired();
        builder.Property(m => m.Col).IsRequired();
        builder.Property(m => m.Stone).HasConversion<int>().IsRequired();
        builder.Property(m => m.PlayedAt).IsRequired();

        builder.HasIndex(m => new { m.GameId, m.Ply }).IsUnique();
    }
}
