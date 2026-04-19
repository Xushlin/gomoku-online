using Gomoku.Domain.Users;
using Gomoku.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gomoku.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="User"/> 聚合根的 EF 映射。<see cref="UserId"/> 用 <see cref="UserIdConverter"/>
/// (Guid→Guid,无 record-class sanitize 问题)。<see cref="Email"/> / <see cref="Username"/>
/// 用 <c>OwnsOne</c> 映射为同表 inline 列;唯一索引通过 navigation 表达式建立。
/// Username 列走 SQLite <c>NOCASE</c> 排序实现大小写不敏感的唯一约束。
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion<UserIdConverter>()
            .ValueGeneratedNever();

        builder.OwnsOne(u => u.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("Email")
                .HasMaxLength(254)
                .IsRequired();
            email.HasIndex(e => e.Value)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");
        });
        builder.Navigation(u => u.Email).IsRequired();

        builder.OwnsOne(u => u.Username, username =>
        {
            username.Property(n => n.Value)
                .HasColumnName("Username")
                .HasMaxLength(20)
                .UseCollation("NOCASE")
                .IsRequired();
            username.HasIndex(n => n.Value)
                .IsUnique()
                .HasDatabaseName("IX_Users_Username");
        });
        builder.Navigation(u => u.Username).IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(u => u.Rating).IsRequired();
        builder.Property(u => u.GamesPlayed).IsRequired();
        builder.Property(u => u.Wins).IsRequired();
        builder.Property(u => u.Losses).IsRequired();
        builder.Property(u => u.Draws).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.IsBot)
            .IsRequired()
            .HasDefaultValue(false);
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasMany(u => u.RefreshTokens)
            .WithOne()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(User.RefreshTokens))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
