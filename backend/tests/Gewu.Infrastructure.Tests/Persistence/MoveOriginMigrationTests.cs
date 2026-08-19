using System.Globalization;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using PersistedMove = Gewu.Domain.Rooms.Move;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gewu.Infrastructure.Tests.Persistence;

/// <summary>
/// `AddMoveOrigin` —— 给 `Moves` 加可空的起点两列。
/// <para>
/// 这一条比前两条迁移安全得多(纯增量、无缺省值、无数据要搬),但它仍然值得一条测试:
/// **迁移是本仓库唯一会在别人机器上按原样跑一遍的东西。** 判据是既有的落子记录一字不变。
/// </para>
/// </summary>
public sealed class MoveOriginMigrationTests : IAsyncLifetime
{
    /// <summary>加起点列之前的那一站。</summary>
    private const string BeforeOrigin = "DropUserRatingColumns";

    private static readonly DateTime Now = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);

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

    private async Task<HashSet<string>> MoveColumnsAsync()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seatsAreATable = await RoomShape.SeatsAreATableAsync(_connection);
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM pragma_table_info('Moves')";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }
        return names;
    }

    /// <summary>
    /// EF 在 SQLite 上把 Guid 存成**大写** TEXT,而 SQLite 的 TEXT 比较是大小写敏感的 ——
    /// 手写 SQL 里用 .NET 默认的小写字面量,外键就对不上(症状是 FOREIGN KEY constraint failed,
    /// 或者更坏:插进去了但查不到)。
    /// </summary>
    private static string Sql(Guid id) => id.ToString().ToUpperInvariant();

    /// <summary>造一局有三步棋的房间,直接走原生 SQL —— 中间那一站的模型还没有起点列。</summary>
    private async Task SeedThreeMovesAsync(Guid gameId)
    {
        var roomId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var side = await MoveSideColumn.DetectAsync(_connection);
        var seatsAreATable = await RoomShape.SeatsAreATableAsync(_connection);
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO Users (Id, Email, Username, PasswordHash, IsActive, IsBot, CreatedAt, RowVersion)
            VALUES ('{Sql(hostId)}', 'seed@example.com', 'Seed', 'x', 1, 0, '{Now:o}', randomblob(16));

            {RoomShape.InsertRoom(seatsAreATable, Sql(roomId), Sql(hostId), Now.ToString("o"))}

            INSERT INTO Games (Id, RoomId, StartedAt, CurrentTurn, RowVersion)
            VALUES ('{Sql(gameId)}', '{Sql(roomId)}', '{Now:o}', 1, randomblob(16));

            INSERT INTO Moves (Id, GameId, Ply, Row, Col, {side.Name}, PlayedAt) VALUES
              ('{Sql(Guid.NewGuid())}', '{Sql(gameId)}', 1, 7,  7, {side.First}, '{Now:o}'),
              ('{Sql(Guid.NewGuid())}', '{Sql(gameId)}', 2, 8,  8, {side.Second}, '{Now.AddSeconds(1):o}'),
              ('{Sql(Guid.NewGuid())}', '{Sql(gameId)}', 3, 7,  8, {side.First}, '{Now.AddSeconds(2):o}');
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Existing_moves_survive_the_migration_unchanged()
    {
        await using var db = NewContext();
        await MigrateToAsync(db, BeforeOrigin);
        var gameId = Guid.NewGuid();
        await SeedThreeMovesAsync(gameId);

        await MigrateToAsync(db);

        var moves = await db.Set<PersistedMove>()
            .Where(m => m.GameId == gameId)
            .OrderBy(m => m.Ply)
            .AsNoTracking()
            .ToListAsync();

        moves.Should().HaveCount(3);
        moves.Select(m => (m.Ply, m.Row, m.Col, m.Seat))
            .Should().Equal((1, 7, 7, BoardSeats.FirstSeat), (2, 8, 8, BoardSeats.SecondSeat), (3, 7, 8, BoardSeats.FirstSeat));
    }

    [Fact]
    public async Task Existing_moves_get_a_null_origin_not_a_zero_one()
    {
        // 若 EF 生成的是 `defaultValue: 0`,每一步既有落子都会变成「从 (0,0) 走过来」——
        // 那是一个合法值,所以不会报错,只会在将来某天看起来很奇怪。可空是唯一诚实的答案。
        await using var db = NewContext();
        await MigrateToAsync(db, BeforeOrigin);
        var gameId = Guid.NewGuid();
        await SeedThreeMovesAsync(gameId);

        await MigrateToAsync(db);

        var moves = await db.Set<PersistedMove>().Where(m => m.GameId == gameId).AsNoTracking().ToListAsync();

        moves.Should().OnlyContain(m => m.FromRow == null && m.FromCol == null);
        moves.Should().OnlyContain(m => m.FromPosition() == null);
    }

    [Fact]
    public async Task A_move_with_an_origin_round_trips()
    {
        await using var db = NewContext();
        await MigrateToAsync(db);
        var columns = await MoveColumnsAsync();
        columns.Should().Contain(new[] { "FromRow", "FromCol" });

        // 走子类棋种存进去、读出来,起点不丢。用聚合根的正常路径,不绕过它。
        var host = User.Register(
            UserId.NewId(), new Email("a@example.com"), new Username("Alice"), "h", Now);
        var guest = User.Register(
            UserId.NewId(), new Email("b@example.com"), new Username("Bob"), "h", Now);
        db.Users.AddRange(host, guest);
        var room = Room.Create(RoomId.NewId(), "origin room", host.Id, Now, GameKeys.Gomoku);
        room.JoinAsPlayer(guest.Id, Now.AddSeconds(1), BuiltInGameRules.Gomoku);
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        // 直接构造一条带起点的记录:五子棋规则会拒绝带起点的走子(它是落子类),
        // 而本用例验的是**持久化层**存不存得住,不是规则接不接受。
        //
        // `ExecuteSqlAsync` 而不是 `ExecuteSqlRawAsync`:后者把插值直接拼进 SQL,编译器
        // 为此报 EF1002。这里的值全是自己造的,注入风险为零,但一条长期存在的 warning
        // 会让下一条真正要紧的 warning 淹没在噪声里。
        var moveId = Sql(Guid.NewGuid());
        var gameId = Sql(room.Game!.Id);
        var playedAt = Now.ToString("o", CultureInfo.InvariantCulture);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO Moves (Id, GameId, Ply, FromRow, FromCol, Row, Col, Seat, PlayedAt)
            VALUES ({moveId}, {gameId}, 1, 3, 4, 5, 6, 0, {playedAt});
            """);

        var stored = await db.Set<PersistedMove>()
            .AsNoTracking()
            .SingleAsync(m => m.GameId == room.Game!.Id);

        stored.FromRow.Should().Be(3);
        stored.FromCol.Should().Be(4);
        stored.FromPosition().Should().Be(new Position(3, 4));
        stored.ToPosition().Should().Be(new Position(5, 6));
        stored.ToPlayedMove().Should().Be(PlayedMove.Positional(new Position(3, 4), new Position(5, 6), BoardSeats.FirstSeat));
    }

    [Fact]
    public async Task Rolling_back_drops_the_columns_and_keeps_the_moves()
    {
        await using var db = NewContext();
        await MigrateToAsync(db, BeforeOrigin);
        var gameId = Guid.NewGuid();
        await SeedThreeMovesAsync(gameId);
        await MigrateToAsync(db);

        await MigrateToAsync(db, BeforeOrigin);

        var columns = await MoveColumnsAsync();
        columns.Should().NotContain("FromRow");
        columns.Should().NotContain("FromCol");

        var seatsAreATable = await RoomShape.SeatsAreATableAsync(_connection);
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM Moves WHERE GameId = '{Sql(gameId)}'";
        var remaining = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        remaining.Should().Be(3, "回滚只该丢掉两列,不该丢掉走子记录");
    }
}
