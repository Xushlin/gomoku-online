using Gewu.Domain.Idioms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="IdiomChar"/> 的 EF 映射 —— 成语的字级反查索引。
/// <para>
/// 两个复合索引,列序相反,对应两种相反的访问模式:纵横生成固定字、变位置
/// (<c>(Char, Position)</c> 前缀就是字);成语接龙固定位置 0、变字
/// (<c>(Position, Char)</c> 前缀就是位置)。少一个,另一种查询就得扫。
/// </para>
/// </summary>
public sealed class IdiomCharConfiguration : IEntityTypeConfiguration<IdiomChar>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdiomChar> builder)
    {
        builder.ToTable("IdiomChars");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.IdiomId).IsRequired();
        builder.Property(c => c.Position).IsRequired();
        builder.Property(c => c.Char).IsRequired();

        builder.HasIndex(c => new { c.Char, c.Position });
        builder.HasIndex(c => new { c.Position, c.Char });
    }
}
