using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using FluentAssertions;
using Gewu.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gewu.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>AddRoomSeats</c> 的回填与回滚。
/// <para>
/// EF 生成的版本有两处错,而两处都不会报错:它把两列**先删再建表**(回填无从下手,存量座位全丢),
/// 而它的 <c>Down</c> 用 <c>defaultValue: Guid.Empty</c> 把 <c>BlackPlayerId</c> 加回来
/// (每个房间的黑方变成空 GUID)。所以两个方向都手写,两个方向都有断言。
/// </para>
/// </summary>
public class RoomSeatsMigrationTests : IAsyncDisposable
{
    private const string Previous = "RenameMoveStoneToSeat";
    private const string Target = "AddRoomSeats";

    private static readonly DateTime Now = new(2026, 8, 19, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    private static string Sql(Guid id) => id.ToString().ToUpperInvariant();

    private async Task MigrateToAsync(string target)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        await using var db = new AppDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync(target);
    }

    private async Task ExecAsync(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<List<(long Index, string UserId)>> SeatsAsync(Guid roomId)
    {
        var rows = new List<(long, string)>();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            $"""SELECT "Index", UserId FROM RoomSeats WHERE RoomId = '{Sql(roomId)}' ORDER BY "Index";""";
        await using DbDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetInt64(0), reader.GetString(1).ToUpperInvariant()));
        }
        return rows;
    }

    private async Task<List<string?>> RoomColumnsAsync(Guid roomId)
    {
        var rows = new List<string?>();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText =
            $"SELECT BlackPlayerId, WhitePlayerId FROM Rooms WHERE Id = '{Sql(roomId)}';";
        await using DbDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0).ToUpperInvariant());
            rows.Add(reader.IsDBNull(1) ? null : reader.GetString(1).ToUpperInvariant());
        }
        return rows;
    }

    /// <summary>造一个房间,座位按**旧的**两列存。</summary>
    private async Task SeedOldRoomAsync(Guid roomId, Guid blackId, Guid? whiteId)
    {
        await ExecAsync($"""
            INSERT INTO Users (Id, Email, Username, PasswordHash, IsActive, IsBot, CreatedAt, RowVersion)
            VALUES ('{Sql(blackId)}', 'b{blackId:N}@x.local', 'B{blackId:N}', 'x', 1, 0, '{Now:o}', randomblob(16));
            """);
        if (whiteId is Guid w)
        {
            await ExecAsync($"""
                INSERT INTO Users (Id, Email, Username, PasswordHash, IsActive, IsBot, CreatedAt, RowVersion)
                VALUES ('{Sql(w)}', 'w{w:N}@x.local', 'W{w:N}', 'x', 1, 0, '{Now:o}', randomblob(16));
                """);
        }

        var white = whiteId is Guid w2 ? $"'{Sql(w2)}'" : "NULL";
        await ExecAsync($"""
            INSERT INTO Rooms (Id, Name, GameKey, HostUserId, BlackPlayerId, WhitePlayerId, Status, CreatedAt)
            VALUES ('{Sql(roomId)}', 'seeded', 'gomoku', '{Sql(blackId)}', '{Sql(blackId)}', {white}, 1, '{Now:o}');
            """);
    }

    [Fact]
    public async Task Both_players_become_seats_zero_and_one()
    {
        var roomId = Guid.NewGuid();
        var black = Guid.NewGuid();
        var white = Guid.NewGuid();
        await MigrateToAsync(Previous);
        await SeedOldRoomAsync(roomId, black, white);

        await MigrateToAsync(Target);

        // EF 生成的顺序(先删列、再建表)下这里会是空的 —— 而它不会报错。
        (await SeatsAsync(roomId)).Should().Equal((0L, Sql(black)), (1L, Sql(white)));
    }

    [Fact]
    public async Task A_waiting_room_gets_only_seat_zero()
    {
        var roomId = Guid.NewGuid();
        var black = Guid.NewGuid();
        await MigrateToAsync(Previous);
        await SeedOldRoomAsync(roomId, black, whiteId: null);

        await MigrateToAsync(Target);

        // 空座位**不存行**。若回填不带 WHERE,这里会多出一行 UserId 为 NULL 的座位,
        // 而那一列是 NOT NULL —— 迁移会当场炸,所以这条同时钉住了那个 WHERE。
        (await SeatsAsync(roomId)).Should().Equal((0L, Sql(black)));
    }

    [Fact]
    public async Task Rolling_back_carries_the_players_back_instead_of_an_empty_guid()
    {
        var roomId = Guid.NewGuid();
        var black = Guid.NewGuid();
        var white = Guid.NewGuid();
        await MigrateToAsync(Previous);
        await SeedOldRoomAsync(roomId, black, white);
        await MigrateToAsync(Target);

        await MigrateToAsync(Previous);

        // EF 生成的 Down 会让这里读到 00000000-0000-0000-0000-000000000000 ——
        // 同 AddRoomGameKey 的 defaultValue: "" 与 DropUserRatingColumns 的 defaultValue: 0。
        // 回滚路径没人走,直到需要它的那天。
        (await RoomColumnsAsync(roomId)).Should().Equal(Sql(black), Sql(white));
    }

    [Fact]
    public async Task Rolling_back_a_waiting_room_leaves_white_null()
    {
        var roomId = Guid.NewGuid();
        var black = Guid.NewGuid();
        await MigrateToAsync(Previous);
        await SeedOldRoomAsync(roomId, black, whiteId: null);
        await MigrateToAsync(Target);

        await MigrateToAsync(Previous);

        (await RoomColumnsAsync(roomId)).Should().Equal(Sql(black), null);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
