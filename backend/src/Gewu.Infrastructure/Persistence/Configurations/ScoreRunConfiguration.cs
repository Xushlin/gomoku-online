using Gewu.Domain.ScoreRuns;
using Gewu.Domain.Users;
using Gewu.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gewu.Infrastructure.Persistence.Configurations;

/// <summary><see cref="ScoreRun"/> 的 EF 映射。</summary>
public sealed class ScoreRunConfiguration : IEntityTypeConfiguration<ScoreRun>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ScoreRun> builder)
    {
        builder.ToTable("ScoreRuns");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.UserId).HasConversion<UserIdConverter>().IsRequired();
        builder.Property(r => r.GameKey).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Seed).IsRequired();
        builder.Property(r => r.StartedAt).IsRequired();
        builder.Property(r => r.FinishedAt);

        // 三者可空 —— 未结算的 run 没有分数,而 0 是一个合法的分数。
        // 这里**不能**写 IsRequired():显式配置压过 CLR 可空性,而
        // generalize-match-payload 的迁移正是在这上面栽过 —— 类型改了、迁移生成了、
        // 数据库仍在运行时拒收。
        builder.Property(r => r.Score);
        builder.Property(r => r.Lines);
        builder.Property(r => r.Level);

        builder.Property(r => r.RowVersion).IsConcurrencyToken().IsRequired();

        // 查询模式一:按 (id, 所有者) 取一局 —— 所有权是查询条件的一部分,不是取回后再判。
        builder.HasIndex(r => new { r.Id, r.UserId });
        // 查询模式二:榜 —— 按 (游戏, 结算时刻) 过滤后取每人最高分。
        builder.HasIndex(r => new { r.GameKey, r.FinishedAt, r.Score });

        // 用户被删时它的 run 一并消失 —— 一局脱离玩家没有意义,而榜要靠这条外键才能
        // 断言"有 run 就一定有用户名"(GetScoreLeaderboardQueryHandler 的注释依赖它)。
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(r => r.IsFinished);
        builder.Ignore(r => r.Duration);
    }
}
