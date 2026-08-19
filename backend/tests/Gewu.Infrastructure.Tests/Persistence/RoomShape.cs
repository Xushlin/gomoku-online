using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Gewu.Infrastructure.Tests.Persistence;

/// <summary>
/// 房间的座位在 <c>AddRoomSeats</c> 前后**存在两个不同的物理形状**:之前是 <c>Rooms</c> 上的
/// <c>BlackPlayerId</c> / <c>WhitePlayerId</c> 两列,之后是 <c>RoomSeats</c> 表里的行。
/// <para>
/// 迁移测试的原生 SQL 会在两个迁移点上跑同一个 seed,所以它得问一下自己站在哪儿 ——
/// 与 <see cref="MoveSideColumn"/> 同一个理由,同一个做法。
/// </para>
/// <para>
/// 这两个 helper 加在一起说明了一件事:**停在命名的中间迁移站上取数据,代价就是要面对
/// 两套物理形状。** 而那个能力正是 `squash-migration-baseline` 被否掉时要保住的东西。
/// </para>
/// </summary>
internal static class RoomShape
{
    /// <summary>座位是否已经搬进 <c>RoomSeats</c> 表。</summary>
    public static async Task<bool> SeatsAreATableAsync(SqliteConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'RoomSeats';";
        return Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
    }

    /// <summary>
    /// 造一个房间的 SQL:host 坐 0 号座位,1 号座位空着(Waiting)。
    /// </summary>
    /// <param name="seatsAreATable">来自 <see cref="SeatsAreATableAsync"/>。</param>
    /// <param name="roomId">房间 id,已按 SQLite 的大写字面量格式化。</param>
    /// <param name="hostId">host id,同上。</param>
    /// <param name="createdAt">建房时刻,ISO-8601。</param>
    public static string InsertRoom(
        bool seatsAreATable, string roomId, string hostId, string createdAt)
        => seatsAreATable
            ? $"""
                INSERT INTO Rooms (Id, Name, GameKey, HostUserId, Status, CreatedAt)
                VALUES ('{roomId}', 'seeded', 'gomoku', '{hostId}', 1, '{createdAt}');

                INSERT INTO RoomSeats (RoomId, "Index", UserId)
                VALUES ('{roomId}', 0, '{hostId}');
                """
            : $"""
                INSERT INTO Rooms (Id, Name, GameKey, HostUserId, BlackPlayerId, WhitePlayerId, Status, CreatedAt)
                VALUES ('{roomId}', 'seeded', 'gomoku', '{hostId}', '{hostId}', NULL, 1, '{createdAt}');
                """;
}
