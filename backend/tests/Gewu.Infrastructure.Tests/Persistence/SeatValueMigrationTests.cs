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
/// <c>RenameMoveStoneToSeat</c> 的**数值**那一半。
/// <para>
/// EF 只生成了一句改名。少掉的位移不会报错 —— 它把每一步历史的出手方和每一局的当前轮次
/// 整个错开一位,表现为棋盘颜色反转、结算赢家错人。所以这里断言的不是"迁移跑通了",
/// 而是"跑完之后那些数还是原来那个意思"。
/// </para>
/// <para>
/// 停在**中间那个迁移**上取数据,是因为位移只在两侧之间可观测:跑完之后源值已经不在了,
/// 断言就只能自证。同 <c>UserGameStatsBackfillTests</c> / <c>MoveOriginMigrationTests</c> 的做法。
/// </para>
/// </summary>
public class SeatValueMigrationTests : IAsyncDisposable
{
    private const string Previous = "AddScoreRuns";
    private const string Target = "RenameMoveStoneToSeat";

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
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(target);
    }

    private async Task ExecAsync(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<List<(int Ply, long Value)>> ReadAsync(string sql)
    {
        var rows = new List<(int, long)>();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await using DbDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetInt32(0), reader.GetInt64(1)));
        }
        return rows;
    }

    /// <summary>GUID 在 SQLite 里存的是大写字面量 —— 小写会让外键对不上。</summary>
    private static string Sql(Guid id) => id.ToString().ToUpperInvariant();

    /// <summary>造一局两步的历史,出手方按**旧的**棋色底层值存(Black=1、White=2)。</summary>
    private async Task SeedOldShapeAsync(Guid gameId)
    {
        var roomId = Guid.NewGuid();
        var hostId = Guid.NewGuid();
        var now = new DateTime(2026, 8, 18, 0, 0, 0, DateTimeKind.Utc);
        await ExecAsync($"""
            INSERT INTO Users (Id, Email, Username, PasswordHash, IsActive, IsBot, CreatedAt, RowVersion)
            VALUES ('{Sql(hostId)}', 'seat@probe.local', 'SeatProbe', 'x', 1, 0, '{now:o}', randomblob(16));

            INSERT INTO Rooms (Id, Name, GameKey, HostUserId, BlackPlayerId, WhitePlayerId, Status, CreatedAt)
            VALUES ('{Sql(roomId)}', 'seeded', 'gomoku', '{Sql(hostId)}', '{Sql(hostId)}', NULL, 1, '{now:o}');

            INSERT INTO Games (Id, RoomId, StartedAt, CurrentTurn, RowVersion)
            VALUES ('{Sql(gameId)}', '{Sql(roomId)}', '{now:o}', 2, randomblob(16));

            INSERT INTO Moves (Id, GameId, Ply, Row, Col, Stone, PlayedAt) VALUES
              ('{Sql(Guid.NewGuid())}', '{Sql(gameId)}', 1, 7, 7, 1, '{now:o}'),
              ('{Sql(Guid.NewGuid())}', '{Sql(gameId)}', 2, 8, 8, 2, '{now.AddSeconds(1):o}');
            """);
    }

    [Fact]
    public async Task Existing_moves_keep_meaning_the_same_side_after_the_shift()
    {
        var gameId = Guid.NewGuid();
        await MigrateToAsync(Previous);
        await SeedOldShapeAsync(gameId);

        await MigrateToAsync(Target);

        // Black(1) 是先手 → 0 号座位;White(2) → 1 号座位。位移漏掉的话这里读到 1 和 2,
        // 也就是每一步都归给了另一个座位。
        var moves = await ReadAsync("SELECT Ply, Seat FROM Moves ORDER BY Ply;");
        moves.Should().Equal((1, 0L), (2, 1L));
    }

    [Fact]
    public async Task The_current_turn_keeps_meaning_the_same_seat()
    {
        var gameId = Guid.NewGuid();
        await MigrateToAsync(Previous);
        await SeedOldShapeAsync(gameId);

        await MigrateToAsync(Target);

        // 这一列的存储类型没变,所以 EF 的生成器对它一个字都没写 —— 它是这次迁移里
        // 最容易漏、漏了最不响的那一处。存的是 White(2),迁移后应当是 1 号座位。
        var turns = await ReadAsync("SELECT 0, CurrentTurn FROM Games;");
        turns.Should().Equal((0, 1L));
    }

    [Fact]
    public async Task Rolling_back_restores_the_old_stone_values_not_off_by_one()
    {
        var gameId = Guid.NewGuid();
        await MigrateToAsync(Previous);
        await SeedOldShapeAsync(gameId);
        await MigrateToAsync(Target);

        await MigrateToAsync(Previous);

        // 回滚路径没人走,直到需要它的那天 —— 与 DropUserRatingColumns 的 Down 同一条理由。
        var moves = await ReadAsync("SELECT Ply, Stone FROM Moves ORDER BY Ply;");
        moves.Should().Equal((1, 1L), (2, 2L));

        var turns = await ReadAsync("SELECT 0, CurrentTurn FROM Games;");
        turns.Should().Equal((0, 2L));
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
