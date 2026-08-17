using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 一步棋从「必然有格子」变成「位置类 或 文本类」:<c>Row</c> / <c>Col</c> 加宽为可空,
    /// 新增 <c>Text</c>。成语接龙的一步是一个成语,它没有格子。
    /// </summary>
    public partial class AddMoveTextPayload : Migration
    {
        /// <summary>
        /// 表名即错误信息 —— 约束失败时 SQLite 会把它打出来。
        /// </summary>
        private const string GuardTable =
            "__rollback_refused_AddMoveTextPayload_would_destroy_textual_moves";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite 上「把列改为可空」是一次非原子的表重建(与 DropUserRatingColumns 同一条
            // 注意事项)。这里是加宽,既有行一字不动,新列为 NULL。
            migrationBuilder.AlterColumn<int>(
                name: "Row",
                table: "Moves",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Col",
                table: "Moves",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "Moves",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // EF 生成的 Down 在这里写的是 `defaultValue: 0`,并且直接 DropColumn("Text")。
            // 那会把每一步成语**静默**变成一步下在 (0,0) 的棋,内容随列一起消失 —— 与
            // add-per-game-rating 那次 `AddColumn(defaultValue: 0)` 是同一个错误,
            // 而没有人会走回滚路径,直到他需要走。
            //
            // 收窄一列而底下有装不进去的数据时,唯一诚实的动作是拒绝。
            // 用一个带 CHECK 的临时表做断言:存在文本类记录时 INSERT 违反约束,迁移中止;
            // 不存在时 INSERT 一行都不写,表随即删掉。
            migrationBuilder.Sql(
                $"CREATE TABLE \"{GuardTable}\" (ok INTEGER NOT NULL CHECK (ok = 1));");
            migrationBuilder.Sql(
                $"INSERT INTO \"{GuardTable}\" (ok) " +
                "SELECT 2 WHERE EXISTS (SELECT 1 FROM \"Moves\" WHERE \"Text\" IS NOT NULL);");
            migrationBuilder.Sql($"DROP TABLE \"{GuardTable}\";");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "Moves");

            migrationBuilder.AlterColumn<int>(
                name: "Row",
                table: "Moves",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Col",
                table: "Moves",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
