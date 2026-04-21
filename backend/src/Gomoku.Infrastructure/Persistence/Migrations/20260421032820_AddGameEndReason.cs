using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gomoku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameEndReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EndReason",
                table: "Games",
                type: "INTEGER",
                nullable: true);

            // 回填历史数据:所有已 Finished 的对局(Result != NULL)都是连五胜
            //(连五是 add-timeout-resign 之前唯一已实现的结束路径)。
            // 未结束的 Games.Result 仍为 NULL,对应 EndReason 也保持 NULL。
            migrationBuilder.Sql("UPDATE Games SET EndReason = 0 WHERE Result IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "Games");
        }
    }
}
