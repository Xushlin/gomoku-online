using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Move = Gewu.Domain.Rooms.Move;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary>
/// <see cref="Move"/>(对局内一步棋)的 EF 映射。<c>(GameId, Ply)</c> 唯一。
/// 一步棋携带**恰好一种**载荷:位置类 <c>(FromRow, FromCol) -> (Row, Col)</c>,或文本类 <c>Text</c>。
/// </summary>
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
        // 四个坐标列全部可空。起点为空是落子类(五子棋 / 一字棋),终点也为空是文本类
        // (成语接龙 —— 它的一步没有格子)。
        //
        // Row / Col 上此前有 .IsRequired()。把 CLR 类型改成 int? 之后它**没有报错也没有失效**
        // —— 显式的 IsRequired 压过 CLR 可空性,于是列仍然是 NOT NULL,生成的迁移里也看不出
        // 少了什么。类型改完、编译通过、迁移干净,而数据库会在插入第一条文本类记录时才拒绝。
        // 删掉它们是本变更真正动到 schema 的地方。
        builder.Property(m => m.FromRow);
        builder.Property(m => m.FromCol);
        builder.Property(m => m.Row);
        builder.Property(m => m.Col);
        // 词典里最长的成语 15 字;64 留足余量,同时挡住把这一列当成自由文本用。
        builder.Property(m => m.Text).HasMaxLength(64);
        builder.Property(m => m.Stone).HasConversion<int>().IsRequired();
        builder.Property(m => m.PlayedAt).IsRequired();

        builder.HasIndex(m => new { m.GameId, m.Ply }).IsUnique();
    }
}
