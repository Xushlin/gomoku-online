using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using Gomoku.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gomoku.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Room"/> 聚合根的 EF 映射。<c>_spectators</c> (List&lt;RoomSpectator&gt;)
/// 与 <c>_chatMessages</c> 通过 navigation + backing field 暴露给 EF;<c>Game</c> 是 1:1
/// 导航。子实体分别有独立 <see cref="IEntityTypeConfiguration{TEntity}"/>。
/// </summary>
public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion<RoomIdConverter>()
            .ValueGeneratedNever();

        builder.Property(r => r.Name).HasMaxLength(50).IsRequired();

        builder.Property(r => r.HostUserId).HasConversion<UserIdConverter>().IsRequired();
        builder.Property(r => r.BlackPlayerId).HasConversion<UserIdConverter>().IsRequired();
        builder.Property(r => r.WhitePlayerId)
            .HasConversion(v => v.HasValue ? v.Value.Value : (Guid?)null,
                           v => v.HasValue ? new UserId(v.Value) : (UserId?)null);

        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.LastUrgeAt);
        builder.Property(r => r.LastUrgeByUserId)
            .HasConversion(v => v.HasValue ? v.Value.Value : (Guid?)null,
                           v => v.HasValue ? new UserId(v.Value) : (UserId?)null);

        // Game: 1:1,外键在 Game 上
        builder.HasOne(r => r.Game)
            .WithOne()
            .HasForeignKey<Game>(g => g.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        // _spectators backing field → RoomSpectators 表
        builder.HasMany<RoomSpectator>("_spectators")
            .WithOne()
            .HasForeignKey(s => s.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation("_spectators")!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // _chatMessages backing field → ChatMessages 表
        builder.HasMany<ChatMessage>("_chatMessages")
            .WithOne()
            .HasForeignKey(c => c.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation("_chatMessages")!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // 不映射公共投影属性 Spectators / ChatMessages;EF 只看 backing field。
        builder.Ignore(r => r.Spectators);
        builder.Ignore(r => r.ChatMessages);
    }
}
