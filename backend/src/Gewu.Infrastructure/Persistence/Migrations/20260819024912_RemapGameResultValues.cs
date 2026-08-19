using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <c>GameResult</c> 从 <c>Ongoing / BlackWin / WhiteWin / Draw</c> 变成
    /// <c>Ongoing / Decided / Draw</c>,于是存下来的 <c>2</c>(旧 <c>WhiteWin</c>)要变成
    /// <c>1</c>(<c>Decided</c>)。<c>0</c> 与 <c>3</c> 一字不动。
    /// <para>
    /// <b>EF 生成的这个迁移是完全空的。</b> 列的类型、可空性、约束一个都没变 —— 变的只是那些
    /// 数字的**含义**。迁移生成器看的是模型,不是语义,所以一次纯值域重映射对它是隐形的。
    /// 这与 <c>RenameMoveStoneToSeat</c> 里 <c>Games.CurrentTurn</c> 的位移是同一类:那次
    /// 存储类型也没变,生成器同样什么都没写,而漏掉它会让每一局的先后手整个翻过来。
    /// </para>
    /// </summary>
    public partial class RemapGameResultValues : Migration
    {
        /// <summary>Down 用来拒绝装不下的数据的临时表。</summary>
        private const string GuardTable = "__game_result_rollback_guard";

        /// <summary>
        /// CHECK 约束的**名字** —— 它才是错误信息。
        /// <para>
        /// <c>AddMoveTextPayload</c> 的同款守卫把这句话写成「表名就是错误信息」,而那没有被量过。
        /// SQLite 报的是 <c>CHECK constraint failed: ok = 1</c> —— 只有约束表达式,**没有表名**。
        /// 那次的测试只断言了异常类型,所以没人发现注释与实际不符。
        /// </para>
        /// <para>
        /// 给约束**起名**之后 SQLite 报的就是这个名字,于是"信息在错误里"这句话才成立。
        /// 那个已合并的迁移不动(已合并的迁移不改是硬规矩,而且它的拒绝本身是对的,
        /// 差的只是诊断信息);这里把机制修对,并由一条断言消息内容的测试钉住。
        /// </para>
        /// </summary>
        private const string GuardConstraint =
            "rollback_refused_a_finished_game_was_won_from_a_seat_the_old_enum_cannot_express";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 旧 WhiteWin(2)→ Decided(1)。赢家是谁本来就写在 WinnerUserId 上,
            // 所以这一步不丢信息 —— 它删掉的正是那份副本。
            migrationBuilder.Sql("UPDATE \"Games\" SET \"Result\" = 1 WHERE \"Result\" = 2;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚要把颜色**算回来**:赢家坐 1 号座位的那些局是旧的 WhiteWin。
            // 这也正是本次改动的论据 —— 那个颜色一直是可以从座位算出来的,即第二份真源。
            //
            // 先拒绝装不下的数据。旧枚举只有两个带颜色的胜负值,所以"赢家坐 2 号座位"
            // 在它里面**没有表示**。今天没有三座位棋种,这条抛不出来;但收窄一个值域而
            // 底下有装不进去的数据时,唯一诚实的动作是拒绝,而不是挑一个值写进去 ——
            // 与 AddMoveTextPayload 的 Down 同一条纪律,同一种做法(带 CHECK 的临时表,
            // 有违规行时 INSERT 失败、迁移中止;没有时一行都不写)。
            migrationBuilder.Sql(
                $"CREATE TABLE \"{GuardTable}\" (ok INTEGER NOT NULL " +
                $"CONSTRAINT {GuardConstraint} CHECK (ok = 1));");
            migrationBuilder.Sql(
                $"INSERT INTO \"{GuardTable}\" (ok) SELECT 2 WHERE EXISTS (" +
                "SELECT 1 FROM \"Games\" g WHERE g.\"Result\" = 1 AND g.\"WinnerUserId\" IS NOT NULL " +
                "AND NOT EXISTS (SELECT 1 FROM \"RoomSeats\" s WHERE s.\"RoomId\" = g.\"RoomId\" " +
                "AND s.\"UserId\" = g.\"WinnerUserId\" AND s.\"Index\" IN (0, 1)));");
            migrationBuilder.Sql($"DROP TABLE \"{GuardTable}\";");

            migrationBuilder.Sql(
                "UPDATE \"Games\" SET \"Result\" = 2 WHERE \"Result\" = 1 AND \"WinnerUserId\" IN (" +
                "SELECT s.\"UserId\" FROM \"RoomSeats\" s WHERE s.\"RoomId\" = \"Games\".\"RoomId\" " +
                "AND s.\"Index\" = 1);");
        }
    }
}
