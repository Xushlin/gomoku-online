using Gewu.Domain.Rooms;
using Gewu.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="RoomSeat"/> 的 EF 映射。主键就是 <c>(RoomId, Index)</c> —— 一个座位号在一个房间里
/// 只能有一行,这条约束由主键本身给,不需要额外的唯一索引。
/// <para>
/// 另有 <c>(RoomId, UserId)</c> 唯一索引:同一个人不能在同一房间坐两个座位。
/// </para>
/// </summary>
public sealed class RoomSeatConfiguration : IEntityTypeConfiguration<RoomSeat>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RoomSeat> builder)
    {
        builder.ToTable("RoomSeats");

        builder.HasKey(s => new { s.RoomId, s.Index });

        builder.Property(s => s.RoomId).HasConversion<RoomIdConverter>().IsRequired();
        builder.Property(s => s.Index).IsRequired();
        builder.Property(s => s.UserId).HasConversion<UserIdConverter>().IsRequired();

        builder.HasIndex(s => new { s.RoomId, s.UserId }).IsUnique();
    }
}
