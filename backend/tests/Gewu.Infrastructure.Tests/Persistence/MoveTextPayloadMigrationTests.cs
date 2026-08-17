using Gewu.Domain.Enums;
using PersistedMove = Gewu.Domain.Rooms.Move;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gewu.Infrastructure.Tests.Persistence;

/// <summary>
/// `AddMoveTextPayload` —— 一步棋从「必然有格子」变成「位置类 或 文本类」。
/// <para>
/// 值得测的是两头:<c>Up</c> 不能碰既有数据,<c>Down</c> 在装不下的时候必须**拒绝**。
/// EF 为 <c>Down</c> 生成的是 <c>defaultValue: 0</c> —— 那会把每一步成语静默变成一步下在
/// (0,0) 的棋,内容随列消失。<c>add-per-game-rating</c> 已经为同一个错误付过一次账,而
/// 回滚路径没有人走,直到他需要走。
/// </para>
/// </summary>
public sealed class MoveTextPayloadMigrationTests : IAsyncLifetime
{
    /// <summary>加文本载荷之前的那一站。</summary>
    private const string BeforeText = "AddMoveOrigin";

    private static readonly DateTime Now = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

    private SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private AppDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    private static Task MigrateToAsync(AppDbContext db, string? target = null) =>
        db.GetService<IMigrator>().MigrateAsync(target);

    /// <summary>EF 在 SQLite 上把 Guid 存成大写 TEXT,而 TEXT 比较大小写敏感。</summary>
    private static string Sql(Guid id) => id.ToString().ToUpperInvariant();

    private async Task<Guid> SeedPositionalMoveAsync()
    {
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO Users (Id, Email, Username, PasswordHash, IsActive, IsBot, CreatedAt, RowVersion)
            VALUES ('{Sql(hostId)}', 'seed@example.com', 'Seed', 'x', 1, 0, '{Now:o}', randomblob(16));

            INSERT INTO Rooms (Id, Name, GameKey, HostUserId, BlackPlayerId, WhitePlayerId, Status, CreatedAt)
            VALUES ('{Sql(roomId)}', 'seeded', 'gomoku', '{Sql(hostId)}', '{Sql(hostId)}', NULL, 1, '{Now:o}');

            INSERT INTO Games (Id, RoomId, StartedAt, CurrentTurn, RowVersion)
            VALUES ('{Sql(gameId)}', '{Sql(roomId)}', '{Now:o}', 1, randomblob(16));

            INSERT INTO Moves (Id, GameId, Ply, Row, Col, Stone, PlayedAt) VALUES
              ('{Sql(Guid.NewGuid())}', '{Sql(gameId)}', 1, 7, 7, 1, '{Now:o}');
            """;
        await cmd.ExecuteNonQueryAsync();
        return gameId;
    }

    private async Task InsertTextualMoveAsync(Guid gameId)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO Moves (Id, GameId, Ply, Row, Col, Text, Stone, PlayedAt)
            VALUES ('{Sql(Guid.NewGuid())}', '{Sql(gameId)}', 2, NULL, NULL, '一心一意', 2, '{Now.AddSeconds(1):o}');
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Existing_moves_survive_the_widening_unchanged()
    {
        await using var db = NewContext();
        await MigrateToAsync(db, BeforeText);
        var gameId = await SeedPositionalMoveAsync();

        await MigrateToAsync(db);

        var move = await db.Set<PersistedMove>()
            .Where(m => m.GameId == gameId).AsNoTracking().SingleAsync();

        move.Ply.Should().Be(1);
        move.Row.Should().Be(7);
        move.Col.Should().Be(7);
        move.Stone.Should().Be(Stone.Black);
        move.Text.Should().BeNull();
    }

    [Fact]
    public async Task A_textual_move_can_be_stored_without_any_coordinates()
    {
        // The whole point: before this migration the columns were NOT NULL, so this
        // insert failed at the database even though the CLR type was already `int?`.
        await using var db = NewContext();
        await MigrateToAsync(db);
        var gameId = await SeedPositionalMoveAsync();

        await InsertTextualMoveAsync(gameId);

        var spoken = await db.Set<PersistedMove>()
            .Where(m => m.GameId == gameId && m.Ply == 2).AsNoTracking().SingleAsync();

        spoken.Text.Should().Be("一心一意");
        spoken.Row.Should().BeNull();
        spoken.Col.Should().BeNull();
    }

    [Fact]
    public async Task Rolling_back_is_fine_while_every_move_is_positional()
    {
        await using var db = NewContext();
        await MigrateToAsync(db);
        var gameId = await SeedPositionalMoveAsync();

        await MigrateToAsync(db, BeforeText);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT Row, Col FROM Moves WHERE GameId = '{Sql(gameId)}'";
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetInt32(0).Should().Be(7);
        reader.GetInt32(1).Should().Be(7);
    }

    [Fact]
    public async Task Rolling_back_refuses_rather_than_flattening_a_textual_move()
    {
        // EF's generated Down would write (0,0) over the idiom and drop the column
        // holding it — a legal-looking value, so nothing would ever complain.
        // Narrowing a column with data that does not fit has one honest answer.
        await using var db = NewContext();
        await MigrateToAsync(db);
        var gameId = await SeedPositionalMoveAsync();
        await InsertTextualMoveAsync(gameId);

        var act = async () => await MigrateToAsync(db, BeforeText);

        await act.Should().ThrowAsync<SqliteException>();

        // And the data is still there: the guard runs before anything is dropped.
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT Text FROM Moves WHERE GameId = '{Sql(gameId)}' AND Ply = 2";
        (await cmd.ExecuteScalarAsync()).Should().Be("一心一意");
    }
}
