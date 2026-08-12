using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using Gomoku.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gomoku.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="RoomSpectator"/>(Room ↔ UserId 联结子实体)的 EF 映射。
/// <c>(RoomId, UserId)</c> 唯一,避免同一用户重复围观同一房间。
/// </summary>
public sealed class RoomSpectatorConfiguration : IEntityTypeConfiguration<RoomSpectator>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RoomSpectator> builder)
    {
        builder.ToTable("RoomSpectators");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.RoomId).HasConversion<RoomIdConverter>().IsRequired();
        builder.Property(s => s.UserId).HasConversion<UserIdConverter>().IsRequired();
        builder.Property(s => s.JoinedAt).IsRequired();

        builder.HasIndex(s => new { s.RoomId, s.UserId }).IsUnique();
    }
}
