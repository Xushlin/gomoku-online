using Gomoku.Domain.Rooms;
using Gomoku.Domain.Users;
using Gomoku.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gomoku.Infrastructure.Persistence.Configurations;

/// <summary><see cref="ChatMessage"/> 的 EF 映射。<c>(RoomId, SentAt)</c> 索引便于将来分页。</summary>
public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.RoomId).HasConversion<RoomIdConverter>().IsRequired();
        builder.Property(c => c.SenderUserId).HasConversion<UserIdConverter>().IsRequired();
        builder.Property(c => c.SenderUsername).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Content).HasMaxLength(500).IsRequired();
        builder.Property(c => c.Channel).HasConversion<int>().IsRequired();
        builder.Property(c => c.SentAt).IsRequired();

        builder.HasIndex(c => new { c.RoomId, c.SentAt });
    }
}
