using Gewu.Domain.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Tests.Persistence;

/// <summary>
/// `AddUserGameStats` 迁移的回填。
/// <para>
/// 这一条不是形式:迁移是本仓库**唯一会在别人机器上按原样跑一遍**的东西,写错了不影响我,
/// 影响下一个 clone 的人。而 EF 只知道"建一张空表",不知道这张表要接管另一张表的数据 ——
/// 回填那段 SQL 是手写的,所以必须有测试盯着它。
/// </para>
/// <para>
/// 测试直接跑真实迁移(`Database.MigrateAsync`),不是 `EnsureCreated` —— 后者按当前模型建库,
/// 会完全跳过迁移脚本,那样这个测试就什么也没测。
/// </para>
/// </summary>
public sealed class UserGameStatsBackfillTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        // 文件型 in-memory 库:同一连接内多次开关也保留内容,迁移能完整跑完。
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private AppDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    [Fact]
    public async Task Migrating_a_database_with_existing_records_carries_them_to_gomoku()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        // 迁移已经把 seeded bot 账号建了出来(AddBotSupport / AddHardBotAccount),
        // 它们是这个库里天然存在的"迁移前数据"。
        var users = await db.Users.ToListAsync();
        users.Should().NotBeEmpty("seeded bot accounts are part of the migrated schema");

        var stats = await db.UserGameStats.ToListAsync();

        stats.Should().HaveCount(users.Count);
        stats.Should().OnlyContain(s => s.GameKey == "gomoku");
    }

    [Fact]
    public async Task Every_user_keeps_their_numbers_to_the_digit()
    {
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        var users = await db.Users.AsNoTracking().ToListAsync();
        var stats = await db.UserGameStats.AsNoTracking().ToListAsync();

        foreach (var user in users)
        {
            var row = stats.SingleOrDefault(s => s.UserId == user.Id && s.GameKey == "gomoku");
            row.Should().NotBeNull($"user {user.Username.Value} must have been carried over");

            row!.Rating.Should().Be(user.Rating);
            row.GamesPlayed.Should().Be(user.GamesPlayed);
            row.Wins.Should().Be(user.Wins);
            row.Losses.Should().Be(user.Losses);
            row.Draws.Should().Be(user.Draws);
        }
    }

    [Fact]
    public async Task Bot_accounts_are_carried_over_too()
    {
        // bot 对局是计分的(add-ai-opponent 的反套利约束),所以它们的战绩同样是真数据。
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        var botIds = await db.Users.Where(u => u.IsBot).Select(u => u.Id).ToListAsync();
        botIds.Should().NotBeEmpty();

        var statIds = await db.UserGameStats.Select(s => s.UserId).ToListAsync();
        statIds.Should().Contain(botIds);
    }

    [Fact]
    public async Task Row_versions_are_not_all_the_same_value()
    {
        // 并发令牌若全表同值,第一次并发写就会出现两行"看起来没被改过",
        // 乐观并发保护形同虚设。回填用的是 randomblob(16)。
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        var versions = await db.UserGameStats.Select(s => s.RowVersion).ToListAsync();
        versions.Should().OnlyContain(v => v != null && v.Length == 16);
        versions.Select(Convert.ToBase64String).Distinct().Should().HaveCount(versions.Count);
    }

    [Fact]
    public async Task The_backfill_does_not_duplicate_on_a_second_run()
    {
        // 迁移本身不会跑两次(__EFMigrationsHistory 挡着),但回填 SQL 带了 NOT EXISTS 守卫。
        // 这条测试盯的是那个守卫本身 —— 手工重放一次,行数不该变。
        await using var db = NewContext();
        await db.Database.MigrateAsync();
        var before = await db.UserGameStats.CountAsync();

        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO UserGameStats
                (UserId, GameKey, Rating, GamesPlayed, Wins, Losses, Draws, RowVersion)
            SELECT Id, 'gomoku', Rating, GamesPlayed, Wins, Losses, Draws, randomblob(16)
            FROM Users
            WHERE NOT EXISTS (
                SELECT 1 FROM UserGameStats s
                WHERE s.UserId = Users.Id AND s.GameKey = 'gomoku');
            """);

        (await db.UserGameStats.CountAsync()).Should().Be(before);
    }

    [Fact]
    public async Task Users_columns_are_still_there_because_this_is_only_the_expand_half()
    {
        // 本迁移刻意**不**删 Users 上那五列。读者还没切过来,先删就把它们打断了。
        // contract 那一半在下一个变更 —— 这条测试是那次改动的提醒:它该变成相反的断言。
        await using var db = NewContext();
        await db.Database.MigrateAsync();

        var user = await db.Users.FirstAsync();

        user.Rating.Should().BeGreaterThan(0);
    }
}
