using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Gewu.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gewu.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>RemapGameResultValues</c> —— 一次**纯值域**迁移。
/// <para>
/// EF 为它生成的是一个**完全空**的迁移:列的类型、可空性、约束一个都没变,变的只是那些数字的
/// 含义。生成器看的是模型,不是语义。所以这里断言的不是"迁移跑通了"(空迁移永远跑通),
/// 而是"跑完之后每一局的胜负还是原来那个意思"。
/// </para>
/// <para>
/// 与 <c>SeatValueMigrationTests</c> 同一类:那次也是存储类型没变而值要位移,EF 同样什么都没写。
/// </para>
/// </summary>
public class GameResultRemapMigrationTests : IAsyncDisposable
{
    private const string Previous = "AddRoomSeats";
    private const string Target = "RemapGameResultValues";

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
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(target);
    }

    private async Task ExecAsync(string sql)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>GUID 在 SQLite 里存的是大写字面量 —— 小写会让外键对不上。</summary>
    private static string Sql(Guid id) => id.ToString().ToUpperInvariant();

    /// <summary>读每一局的 <c>(Result, WinnerUserId)</c>,按房间名排序。</summary>
    private async Task<List<(string Room, long? Result, string? Winner)>> ReadGamesAsync()
    {
        var rows = new List<(string, long?, string?)>();
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT r.Name, g.Result, g.WinnerUserId
            FROM Games g JOIN Rooms r ON r.Id = g.RoomId
            ORDER BY r.Name;
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return rows;
    }

    /// <summary>
    /// 造一局已结束的对局,<c>Result</c> 按**旧的**底层值写(1 = BlackWin、2 = WhiteWin、3 = Draw)。
    /// </summary>
    private async Task SeedFinishedGameAsync(string name, int oldResult, int? winnerSeat)
    {
        var roomId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await ExecAsync($"""
            INSERT INTO Users (Id, Email, Username, PasswordHash, IsActive, IsBot, CreatedAt, RowVersion)
            VALUES ('{Sql(first)}', 'a{first:N}@x.local', 'A{first:N}', 'x', 1, 0, '{Now:o}', randomblob(16)),
                   ('{Sql(second)}', 'b{second:N}@x.local', 'B{second:N}', 'x', 1, 0, '{Now:o}', randomblob(16));

            INSERT INTO Rooms (Id, Name, GameKey, HostUserId, Status, CreatedAt)
            VALUES ('{Sql(roomId)}', '{name}', 'gomoku', '{Sql(first)}', 2, '{Now:o}');

            INSERT INTO RoomSeats (RoomId, "Index", UserId) VALUES
              ('{Sql(roomId)}', 0, '{Sql(first)}'),
              ('{Sql(roomId)}', 1, '{Sql(second)}');

            INSERT INTO Games (Id, RoomId, StartedAt, EndedAt, Result, WinnerUserId, EndReason, CurrentTurn, RowVersion)
            VALUES ('{Sql(Guid.NewGuid())}', '{Sql(roomId)}', '{Now:o}', '{Now.AddMinutes(5):o}',
                    {oldResult},
                    {(winnerSeat is null ? "NULL" : $"'{Sql(winnerSeat == 0 ? first : second)}'")},
                    0, 0, randomblob(16));
            """);
    }

    /// <summary>三局旧数据:先手胜、后手胜、和局。</summary>
    private async Task SeedAllThreeAsync()
    {
        await MigrateToAsync(Previous);
        await SeedFinishedGameAsync("a-first-seat-won", oldResult: 1, winnerSeat: 0);
        await SeedFinishedGameAsync("b-second-seat-won", oldResult: 2, winnerSeat: 1);
        await SeedFinishedGameAsync("c-drawn", oldResult: 3, winnerSeat: null);
    }

    [Fact]
    public async Task Both_wins_become_decided_and_the_draw_is_untouched()
    {
        await SeedAllThreeAsync();
        var before = await ReadGamesAsync();

        await MigrateToAsync(Target);

        var after = await ReadGamesAsync();
        after.Should().HaveCount(3);

        // 两种胜都变成 Decided(1),和局留在 3。
        after[0].Result.Should().Be(1, "旧 BlackWin 的底层值本来就是 1");
        after[1].Result.Should().Be(1, "旧 WhiteWin(2)必须变成 Decided(1)");
        after[2].Result.Should().Be(3, "和局的底层值刻意没动,所以这一行不该被碰");

        // 赢家一个字都不该变 —— 它才是"谁赢了"的真源,这次改动删的是它的副本。
        for (var i = 0; i < 3; i++)
        {
            after[i].Winner.Should().Be(before[i].Winner, $"第 {i} 局的赢家不该被迁移改动");
        }
    }

    [Fact]
    public async Task Rolling_back_recomputes_the_colour_from_the_seat()
    {
        await SeedAllThreeAsync();
        await MigrateToAsync(Target);

        await MigrateToAsync(Previous);

        var after = await ReadGamesAsync();
        after[0].Result.Should().Be(1, "赢家坐 0 号 → 旧 BlackWin");
        after[1].Result.Should().Be(2, "赢家坐 1 号 → 旧 WhiteWin");
        after[2].Result.Should().Be(3, "和局");
    }

    [Fact]
    public async Task Rolling_back_refuses_a_winner_from_a_third_seat()
    {
        // 旧枚举只有两个带颜色的胜负值,所以"赢家坐 2 号"在它里面**没有表示**。
        // 今天没有三座位棋种,这条走不到真实数据;但收窄一个值域而底下有装不进去的数据时,
        // 唯一诚实的动作是拒绝 —— 挑一个值写进去会静默地把赢家改成别人。
        await MigrateToAsync(Previous);

        var roomId = Guid.NewGuid();
        var players = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        await ExecAsync($"""
            INSERT INTO Users (Id, Email, Username, PasswordHash, IsActive, IsBot, CreatedAt, RowVersion)
            VALUES ('{Sql(players[0])}', 'p0@x.local', 'P0', 'x', 1, 0, '{Now:o}', randomblob(16)),
                   ('{Sql(players[1])}', 'p1@x.local', 'P1', 'x', 1, 0, '{Now:o}', randomblob(16)),
                   ('{Sql(players[2])}', 'p2@x.local', 'P2', 'x', 1, 0, '{Now:o}', randomblob(16));

            INSERT INTO Rooms (Id, Name, GameKey, HostUserId, Status, CreatedAt)
            VALUES ('{Sql(roomId)}', 'three-seats', 'probe', '{Sql(players[0])}', 2, '{Now:o}');

            INSERT INTO RoomSeats (RoomId, "Index", UserId) VALUES
              ('{Sql(roomId)}', 0, '{Sql(players[0])}'),
              ('{Sql(roomId)}', 1, '{Sql(players[1])}'),
              ('{Sql(roomId)}', 2, '{Sql(players[2])}');

            INSERT INTO Games (Id, RoomId, StartedAt, EndedAt, Result, WinnerUserId, EndReason, CurrentTurn, RowVersion)
            VALUES ('{Sql(Guid.NewGuid())}', '{Sql(roomId)}', '{Now:o}', '{Now.AddMinutes(5):o}',
                    1, '{Sql(players[2])}', 0, 0, randomblob(16));
            """);

        await MigrateToAsync(Target);

        var act = async () => await MigrateToAsync(Previous);

        (await act.Should().ThrowAsync<SqliteException>())
            .Which.Message.Should().Contain("rollback_refused");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
