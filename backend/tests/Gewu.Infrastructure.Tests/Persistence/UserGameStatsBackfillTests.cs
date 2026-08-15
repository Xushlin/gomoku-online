using Gewu.Domain.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gewu.Infrastructure.Tests.Persistence;

/// <summary>
/// `AddUserGameStats` 的回填 + `DropUserRatingColumns` 的删列 —— expand / contract 两条迁移。
/// <para>
/// 这一条不是形式:迁移是本仓库**唯一会在别人机器上按原样跑一遍**的东西,写错了不影响我,
/// 影响下一个 clone 的人。而 EF 只知道"建一张空表",不知道这张表要接管另一张表的数据 ——
/// 回填那段 SQL 是手写的,所以必须有测试盯着它。
/// </para>
/// <para>
/// 测试直接跑真实迁移(`IMigrator.MigrateAsync`),不是 `EnsureCreated` —— 后者按当前模型建库,
/// 会完全跳过迁移脚本,那样这个测试就什么也没测。
/// </para>
/// <para>
/// **停在中间那一站是有意的。** 数据在 expand 之后、contract 之前是"两处都有"的状态,而那正是
/// "搬对了没有"唯一能被观察到的时刻:删完列之后源数据已经不在了,再断言就只是在跟自己核对。
/// 所以下面先迁到 `AddUserGameStats` 逐字段比对,再迁到最新确认列真的没了。
/// </para>
/// <para>
/// 那时的 `Users` 五列已经不在 EF 模型里了,所以比对走原生 SQL 而不是实体属性 ——
/// 这也顺带证明了断言看的是**数据库里的字节**,不是 EF 又映射了一遍自己写进去的东西。
/// </para>
/// </summary>
public sealed class UserGameStatsBackfillTests : IAsyncLifetime
{
    /// <summary>expand 那一站:表建好、数据搬完,但 `Users` 的五列还在。</summary>
    private const string AfterExpand = "AddUserGameStats";

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

    /// <summary>迁到指定那一条为止(含);<c>null</c> = 迁到最新。</summary>
    private static Task MigrateToAsync(AppDbContext db, string? target = null) =>
        db.GetService<IMigrator>().MigrateAsync(target);

    /// <summary>迁移前形状下 `Users` 的战绩五列 —— 它们已不在 EF 模型里,只能走原生 SQL。</summary>
    private async Task<Dictionary<Guid, (int Rating, int Games, int Wins, int Losses, int Draws)>>
        ReadLegacyUserStatsAsync()
    {
        var rows = new Dictionary<Guid, (int, int, int, int, int)>();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Rating, GamesPlayed, Wins, Losses, Draws FROM Users";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows[reader.GetGuid(0)] =
                (reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5));
        }
        return rows;
    }

    private async Task<HashSet<string>> ReadUsersColumnNamesAsync()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM pragma_table_info('Users')";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    [Fact]
    public async Task Migrating_a_database_with_existing_records_carries_them_to_gomoku()
    {
        await using var db = NewContext();
        await MigrateToAsync(db, AfterExpand);

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
        await MigrateToAsync(db, AfterExpand);

        var legacy = await ReadLegacyUserStatsAsync();
        legacy.Should().NotBeEmpty();
        var stats = await db.UserGameStats.AsNoTracking().ToListAsync();

        foreach (var (userId, before) in legacy)
        {
            var row = stats.SingleOrDefault(s => s.UserId.Value == userId && s.GameKey == "gomoku");
            row.Should().NotBeNull($"user {userId} must have been carried over");

            row!.Rating.Should().Be(before.Rating);
            row.GamesPlayed.Should().Be(before.Games);
            row.Wins.Should().Be(before.Wins);
            row.Losses.Should().Be(before.Losses);
            row.Draws.Should().Be(before.Draws);
        }
    }

    [Fact]
    public async Task Bot_accounts_are_carried_over_too()
    {
        // bot 对局是计分的(add-ai-opponent 的反套利约束),所以它们的战绩同样是真数据。
        await using var db = NewContext();
        await MigrateToAsync(db, AfterExpand);

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
        await MigrateToAsync(db, AfterExpand);

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
        await MigrateToAsync(db, AfterExpand);
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
    public async Task The_expand_half_leaves_the_source_columns_in_place()
    {
        // expand 刻意**不**删列,于是它是可逆的:回滚只需丢掉新表,战绩仍在 Users 原处。
        await using var db = NewContext();
        await MigrateToAsync(db, AfterExpand);

        var columns = await ReadUsersColumnNamesAsync();

        columns.Should().Contain(new[] { "Rating", "GamesPlayed", "Wins", "Losses", "Draws" });
    }

    [Fact]
    public async Task The_contract_half_drops_the_source_columns_without_losing_the_data()
    {
        // 顺序不能颠倒:先删列再搬数据 = 数据没了。这条测试跑的就是真实顺序。
        await using var db = NewContext();
        await MigrateToAsync(db, AfterExpand);
        var before = await ReadLegacyUserStatsAsync();

        await MigrateToAsync(db);

        var columns = await ReadUsersColumnNamesAsync();
        columns.Should().NotContain("Rating");
        columns.Should().NotContain("GamesPlayed");
        columns.Should().NotContain("Wins");
        columns.Should().NotContain("Losses");
        columns.Should().NotContain("Draws");

        var stats = await db.UserGameStats.AsNoTracking().ToListAsync();
        stats.Should().HaveCount(before.Count);
        foreach (var (userId, legacy) in before)
        {
            var row = stats.Single(s => s.UserId.Value == userId && s.GameKey == "gomoku");
            row.Rating.Should().Be(legacy.Rating);
            row.GamesPlayed.Should().Be(legacy.Games);
        }
    }

    [Fact]
    public async Task Rolling_the_contract_half_back_restores_the_real_numbers_not_zeroes()
    {
        // EF 生成的 Down 只 AddColumn(defaultValue: 0),回滚后每个人的分都会变成 0 ——
        // 数据其实还在 UserGameStats 里,只是没人去取。Down 里手写了一段搬回来的 SQL,
        // 这条测试盯的就是那段。回滚这条路平时没人走,坏了不会有人立刻发现。
        await using var db = NewContext();
        await MigrateToAsync(db);

        // 让至少一行的分明显不是缺省值,否则"搬回来了"和"用了缺省 1200"看起来一样。
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE UserGameStats SET Rating = 1777, GamesPlayed = 42, Wins = 30 WHERE GameKey = 'gomoku'");

        await MigrateToAsync(db, AfterExpand);

        var restored = await ReadLegacyUserStatsAsync();
        restored.Should().NotBeEmpty();
        restored.Values.Should().OnlyContain(v => v.Rating == 1777 && v.Games == 42 && v.Wins == 30);
    }
}
