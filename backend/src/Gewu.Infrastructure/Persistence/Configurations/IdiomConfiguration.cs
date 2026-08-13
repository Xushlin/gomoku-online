using Gewu.Domain.Idioms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Idiom"/> 的 EF 映射。<c>Word</c> 唯一;<c>Characters</c> 级联删除。
/// <c>Tier</c> / <c>TierOverride</c> 以 <c>int</c> 落库,便于 SQL 里直接比较大小。
/// </summary>
public sealed class IdiomConfiguration : IEntityTypeConfiguration<Idiom>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Idiom> builder)
    {
        builder.ToTable("Idioms");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedOnAdd();

        builder.Property(i => i.Word).IsRequired().HasMaxLength(32);
        builder.Property(i => i.Pinyin).IsRequired().HasMaxLength(128);
        builder.Property(i => i.Explanation).IsRequired();
        builder.Property(i => i.Derivation).IsRequired();
        builder.Property(i => i.Example).IsRequired();
        builder.Property(i => i.CharCount).IsRequired();
        builder.Property(i => i.MinCharFrequency).IsRequired();
        builder.Property(i => i.Tier).HasConversion<int>().IsRequired();
        builder.Property(i => i.TierOverride).HasConversion<int?>();

        builder.HasIndex(i => i.Word).IsUnique();

        // 层级过滤是每个生成查询的第一道筛子,单列索引让它先把 96% 的行排除掉。
        builder.HasIndex(i => i.Tier);

        builder.HasMany(i => i.Characters)
            .WithOne()
            .HasForeignKey(c => c.IdiomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Idiom.Characters))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
