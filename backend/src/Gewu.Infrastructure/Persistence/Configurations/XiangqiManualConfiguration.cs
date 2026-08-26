using Gewu.Domain.Manuals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary><see cref="XiangqiManual"/> 的 EF 映射。键就是主键 —— 它来自数据文件,不是自增。</summary>
public sealed class XiangqiManualConfiguration : IEntityTypeConfiguration<XiangqiManual>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<XiangqiManual> builder)
    {
        builder.ToTable("XiangqiManuals");

        builder.HasKey(m => m.Key);
        builder.Property(m => m.Key).HasMaxLength(64);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Grouped).IsRequired();
    }
}
