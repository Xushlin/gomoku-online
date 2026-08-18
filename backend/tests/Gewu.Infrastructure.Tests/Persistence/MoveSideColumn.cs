using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Gewu.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>Moves</c> 表上"出手方"那一列,在 <c>RenameMoveStoneToSeat</c> 前后**列名和取值都不一样**:
/// 之前叫 <c>Stone</c>、存棋色底层值(Black=1、White=2),之后叫 <c>Seat</c>、存座位号(0、1)。
/// <para>
/// 迁移测试的原生 SQL 会在**两个不同的迁移点**上跑同一个 seed(有的停在中间那一站,有的跑到
/// 最新),所以写死任何一种都会有一半用例插不进去。让 seed **自己探测**,而不是让每个调用方
/// 记住自己站在哪儿 —— 后者是那种"加一个用例时忘了传参、于是它悄悄验了别的东西"的形状。
/// </para>
/// <para>
/// 这也顺便说明了为什么 <c>squash-migration-baseline</c> 被否掉:这些用例的价值全在于它们能停在
/// 命名的中间站上,而"停在中间站"这件事本身就意味着它们要面对两套物理形状。
/// </para>
/// </summary>
internal static class MoveSideColumn
{
    /// <summary>探测当前数据库上出手方那一列的名字与两个取值。</summary>
    /// <param name="connection">已打开的连接。</param>
    public static async Task<(string Name, int First, int Second)> DetectAsync(
        SqliteConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Moves') WHERE name = 'Seat';";
        var renamed = System.Convert.ToInt64(await cmd.ExecuteScalarAsync()) > 0;
        return renamed ? ("Seat", 0, 1) : ("Stone", 1, 2);
    }
}
