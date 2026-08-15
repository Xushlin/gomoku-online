using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// expand/contract 的 **contract** 一半:读者已经全部切到 <c>UserGameStats</c>,
    /// 于是 <c>Users</c> 上那五个战绩列可以删了。
    /// <para>
    /// 它 MUST 排在 <c>AddUserGameStats</c> 之后 —— 那一条建表并把数据搬过去,这一条才删源列。
    /// 顺序由迁移时间戳保证(<c>20260815120842</c> &lt; <c>20260815123939</c>)。将来若有人合并 /
    /// 压缩迁移,**这个先后不能颠倒**:先删列再搬数据 = 数据没了。
    /// </para>
    /// <para>
    /// **运行时会打一条警告,它是真的:** SQLite 的 <c>DROP COLUMN</c> 由 EF 降级成"建新表 → 拷贝
    /// → 换名",而那需要 <c>PRAGMA foreign_keys = 0</c>,该语句不能在事务里执行。于是本迁移**不是
    /// 原子的** —— 中途断电会留下半应用状态,需要手工回退。
    /// 现在无所谓(本地库、无生产数据、数据在 <c>UserGameStats</c> 里另有一份),
    /// 但真上生产之前得先备份再跑。记在这里,免得那条警告被当成噪音划过去。
    /// </para>
    /// </summary>
    public partial class DropUserRatingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Draws",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GamesPlayed",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Losses",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Wins",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ---- EF 生成的版本在这里是错的,手工改过 ----
            //
            // 它只 AddColumn(... defaultValue: 0),于是回滚之后每个人的 Rating 都是 **0**、战绩全清 ——
            // 数据其实还在 UserGameStats 里,只是没人去取。这与 AddRoomGameKey 那次 EF 生成
            // `defaultValue: ""` 让所有房间不可玩是同一类 bug:自动生成的缺省值语法上没问题,
            // 语义上是垃圾,而且回滚这条路平时没人走,坏了不会立刻被发现。
            //
            // 所以这里加完列之后显式把数据搬回来。Rating 的缺省取 1200 而不是 0:没有 gomoku 行的
            // 用户(注册后从没下完过一局)搬不回任何东西,而对他们来说 1200 才是正确的初始分。
            migrationBuilder.AddColumn<int>(
                name: "Draws",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GamesPlayed",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Losses",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1200);

            migrationBuilder.AddColumn<int>(
                name: "Wins",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // 用关联子查询而不是 UPDATE ... FROM:后者要 SQLite 3.33+,前者哪个版本都能跑。
            // WHERE EXISTS 让没有 gomoku 行的用户保持上面的缺省值,不被 NULL 覆盖。
            migrationBuilder.Sql(
                """
                UPDATE Users
                SET Rating      = (SELECT s.Rating      FROM UserGameStats s WHERE s.UserId = Users.Id AND s.GameKey = 'gomoku'),
                    GamesPlayed = (SELECT s.GamesPlayed FROM UserGameStats s WHERE s.UserId = Users.Id AND s.GameKey = 'gomoku'),
                    Wins        = (SELECT s.Wins        FROM UserGameStats s WHERE s.UserId = Users.Id AND s.GameKey = 'gomoku'),
                    Losses      = (SELECT s.Losses      FROM UserGameStats s WHERE s.UserId = Users.Id AND s.GameKey = 'gomoku'),
                    Draws       = (SELECT s.Draws       FROM UserGameStats s WHERE s.UserId = Users.Id AND s.GameKey = 'gomoku')
                WHERE EXISTS (
                    SELECT 1 FROM UserGameStats s
                    WHERE s.UserId = Users.Id AND s.GameKey = 'gomoku');
                """);
        }
    }
}
