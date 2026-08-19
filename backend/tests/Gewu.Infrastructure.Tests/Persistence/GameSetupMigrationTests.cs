using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Gewu.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gewu.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>AddGameSetup</c> —— 纯加宽,而这是**核对过**的结论,不是默认它对。
/// <para>
/// 这是本仓库第一个可以直接采用 EF 生成结果的迁移。前面四次各自错在不同的地方
/// (<c>defaultValue: ""</c> / <c>defaultValue: 0</c> / drop-before-create / 值位移隐形),
/// 所以核对本身仍然是必要的那一步 —— 变的只是这一次的结论。这些断言就是那次核对。
/// </para>
/// </summary>
public class GameSetupMigrationTests : IAsyncDisposable
{
    private const string Previous = "RemapGameResultValues";
    private const string Target = "AddGameSetup";

    private static readonly DateTime Now = new(2026, 8, 19, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    private AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }

    private async Task MigrateToAsync(string target)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }

        await using var db = NewContext();
        await db.GetService<IMigrator>().MigrateAsync(target);
    }

    private async Task ExecAsync(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        return await cmd.ExecuteScalarAsync();
    }

    private static string Sql(Guid id) => id.ToString().ToUpperInvariant();

    private readonly Guid _gameId = Guid.NewGuid();

    /// <summary>在**加列之前**造一局进行中的对局。</summary>
    private async Task SeedBeforeAsync()
    {
        await MigrateToAsync(Previous);

        var roomId = Guid.NewGuid();
        var host = Guid.NewGuid();
        await ExecAsync($"""
            INSERT INTO Users (Id, Email, Username, PasswordHash, IsActive, IsBot, CreatedAt, RowVersion)
            VALUES ('{Sql(host)}', 'setup@probe.local', 'SetupProbe', 'x', 1, 0, '{Now:o}', randomblob(16));

            INSERT INTO Rooms (Id, Name, GameKey, HostUserId, Status, CreatedAt)
            VALUES ('{Sql(roomId)}', 'seeded', 'gomoku', '{Sql(host)}', 1, '{Now:o}');

            INSERT INTO RoomSeats (RoomId, "Index", UserId) VALUES ('{Sql(roomId)}', 0, '{Sql(host)}');

            INSERT INTO Games (Id, RoomId, StartedAt, CurrentTurn, RowVersion)
            VALUES ('{Sql(_gameId)}', '{Sql(roomId)}', '{Now:o}', 0, randomblob(16));
            """);
    }

    [Fact]
    public async Task Existing_games_are_untouched_and_get_a_null_setup()
    {
        await SeedBeforeAsync();
        var turnBefore = await ScalarAsync($"SELECT CurrentTurn FROM Games WHERE Id = '{Sql(_gameId)}';");

        await MigrateToAsync(Target);

        (await ScalarAsync($"SELECT CurrentTurn FROM Games WHERE Id = '{Sql(_gameId)}';"))
            .Should().Be(turnBefore, "加一列不该动别的列");
        (await ScalarAsync($"SELECT Setup FROM Games WHERE Id = '{Sql(_gameId)}';"))
            .Should().Be(DBNull.Value, "既有行没有设置 —— MUST NOT 是空字符串");
    }

    [Fact]
    public async Task The_new_column_is_nullable()
    {
        // 若 EF 生成了 `nullable: false` 加 `defaultValue: ""`(它在 AddRoomGameKey 里就是这么干的),
        // 每一局既有对局的 Setup 会变成空字符串 —— 而"没有设置"与"设置是空的"因此不可区分。
        await SeedBeforeAsync();
        await MigrateToAsync(Target);

        var notNull = await ScalarAsync(
            "SELECT \"notnull\" FROM pragma_table_info('Games') WHERE name = 'Setup';");

        notNull.Should().NotBeNull();
        Convert.ToInt32(notNull).Should().Be(0);
    }

    [Fact]
    public async Task Rolling_back_drops_the_column_and_leaves_the_rest()
    {
        await SeedBeforeAsync();
        await MigrateToAsync(Target);

        await MigrateToAsync(Previous);

        (await ScalarAsync(
            "SELECT COUNT(*) FROM pragma_table_info('Games') WHERE name = 'Setup';"))
            .Should().Be(0L);
        (await ScalarAsync($"SELECT COUNT(*) FROM Games WHERE Id = '{Sql(_gameId)}';"))
            .Should().Be(1L, "回滚不该丢掉对局本身");
    }

    [Fact]
    public async Task Rolling_back_across_this_migration_destroys_a_deal()
    {
        // **这条测试替换掉的那一条,写的是一个现在已经不成立的理由。**
        //
        // 原文:「Setup 的唯一读者是需要它的那个棋种的规则,而回滚到本迁移之前意味着那个棋种
        // 在这个构建里还不存在 —— 所以不可能有非 NULL 的行需要保护」,并断言"没有任何内置棋种
        // 会产生非 NULL 的 Setup",注释里写着「斗地主落地那天这条会红,而那正是该重新想一遍
        // 这个 Down 的时刻」。**斗地主落地了,它红了,所以这里是重新想的结果。**
        //
        // 重新想的结论:那个 Down **确实会毁数据**,而且比"回滚到一个没有斗地主的构建"更难看 ——
        // 回滚再前滚一次,列会以全 NULL 回来,房间还在、还像能玩,而规则在下一手抛
        // 「This doudizhu game has no deal recorded」。
        //
        // 那个迁移**不能就地改**:已合并的迁移不改是硬规矩。所以这条测试的作用变了 ——
        // 它不再论证"不需要守卫",而是把后果**演出来**并留在案上。真要修就是加一个新迁移,
        // 而在没有部署、库随时可删的今天,那笔钱不值得花。
        await SeedBeforeAsync();
        await MigrateToAsync(Target);
        await ExecAsync($"UPDATE Games SET Setup = 'AB/CD/EF' WHERE Id = '{Sql(_gameId)}';");

        await MigrateToAsync(Previous);
        await MigrateToAsync(Target);

        (await ScalarAsync($"SELECT COUNT(*) FROM Games WHERE Id = '{Sql(_gameId)}';"))
            .Should().Be(1L, "房间与对局都还在 —— 这正是难看的地方");
        (await ScalarAsync($"SELECT Setup FROM Games WHERE Id = '{Sql(_gameId)}';"))
            .Should().Be(DBNull.Value, "发牌没了,而这一局看起来仍然能继续");
    }

    [Fact]
    public async Task Exactly_one_built_in_game_can_produce_a_non_null_setup()
    {
        // 上面那条演的是后果;这一条盯的是**范围**。今天只有斗地主会写非 NULL 的 Setup,
        // 所以那笔"加一个带守卫的新迁移"的账只涉及一个棋种。第二个棋种要设置的那天这条会红 ——
        // 那时这笔账变大,该重新估。
        var lexicon = new Gewu.Domain.Idioms.InMemoryIdiomLexicon(["一心一意"]);

        Gewu.Domain.Games.NInARow.BuiltInGameRules.All(lexicon)
            .Where(r => r is Gewu.Domain.Games.Abstractions.IDealtGameRules)
            .Should().ContainSingle()
            .Which.GameKey.Should().Be(Gewu.Domain.Games.Abstractions.GameKeys.Doudizhu);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
