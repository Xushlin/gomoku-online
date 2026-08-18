using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 建 <c>ScoreRuns</c> 表 —— 纯新增,不动任何既有列。
    /// <para>
    /// 三个结果列(<c>Score</c> / <c>Lines</c> / <c>Level</c>)是**可空**的,而这一点值得核对:
    /// <c>generalize-match-payload</c> 的迁移正是在这里栽过 —— CLR 类型改成了可空,但
    /// <c>MoveConfiguration</c> 里还留着 <c>.IsRequired()</c>,而**显式配置压过 CLR 可空性**,
    /// 于是迁移干净地生成、数据库在运行时才拒收。<c>ScoreRunConfiguration</c> 对这三列
    /// 刻意不调 <c>IsRequired()</c>。
    /// </para>
    /// <para>
    /// <c>Down</c> 直接 <c>DropTable</c>,而这次那是**对的**,尽管这个仓库为两个错的 <c>Down</c>
    /// 付过账(<c>add-per-game-rating</c> 会把所有人恢复成 0 分,<c>generalize-match-payload</c>
    /// 会把成语变成 (0,0) 的落子)。区别在于那两次都有**别处仍然读得到的数据**要搬回去;
    /// 这次表本身是新的,回滚这个功能就是回滚这些 run,没有第二个去处。
    /// </para>
    /// </summary>
    public partial class AddScoreRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScoreRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Seed = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    Lines = table.Column<int>(type: "INTEGER", nullable: true),
                    Level = table.Column<int>(type: "INTEGER", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScoreRuns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreRuns_GameKey_FinishedAt_Score",
                table: "ScoreRuns",
                columns: new[] { "GameKey", "FinishedAt", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreRuns_Id_UserId",
                table: "ScoreRuns",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreRuns_UserId",
                table: "ScoreRuns",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScoreRuns");
        }
    }
}
