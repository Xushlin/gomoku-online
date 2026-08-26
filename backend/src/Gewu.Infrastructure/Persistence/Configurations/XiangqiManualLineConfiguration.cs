using Gewu.Domain.Manuals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="XiangqiManualLine"/> 的 EF 映射。<c>(ManualKey, Chapter, OrderInChapter)</c>
/// 唯一 —— 那三个字段就是目录里的位置,重复会让「第1局的第2个变化」指向两条线路,
/// 而播种是幂等的,重复只可能来自数据文件里的重复。
/// </summary>
public sealed class XiangqiManualLineConfiguration : IEntityTypeConfiguration<XiangqiManualLine>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<XiangqiManualLine> builder)
    {
        builder.ToTable("XiangqiManualLines");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedOnAdd();

        builder.Property(l => l.ManualKey).IsRequired().HasMaxLength(64);
        builder.Property(l => l.Chapter).IsRequired();
        builder.Property(l => l.OrderInChapter).IsRequired();
        builder.Property(l => l.Title).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Verdict).IsRequired().HasConversion<int>();
        // 盘面串定长 —— 长度由领域挡住,这里把长度也写进列,好让「89 个字符」在库这一层
        // 也是一个错误而不是一段能存下去的数据。
        builder.Property(l => l.StartPosition)
            .IsRequired()
            .HasMaxLength(XiangqiManualLine.BoardStringLength)
            .IsFixedLength();
        builder.Property(l => l.FirstSeat).IsRequired();
        builder.Property(l => l.MovesJson).IsRequired();

        builder
            .HasIndex(l => new { l.ManualKey, l.Chapter, l.OrderInChapter })
            .IsUnique();
    }
}
