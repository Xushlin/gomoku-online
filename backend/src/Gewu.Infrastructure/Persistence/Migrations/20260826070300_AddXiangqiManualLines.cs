using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 古谱线路表。
    /// <para>
    /// <c>Down</c> 整表 drop 在这里是**安全**的,而这不是通例:表里的每一行都能从仓库里
    /// 提交的产物(<c>data/manuals/xiangqi-meihuapu.json</c>)完整复现,播种器还会逐手过
    /// 象棋规则。<c>AddGameSetup</c> 那条延期项之所以是延期项,是因为它 drop 掉的列里装着
    /// **对局产生的**数据,回不来。两者的区别是「数据从哪来」,不是「drop 了什么」。
    /// </para>
    /// </summary>
    public partial class AddXiangqiManualLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "XiangqiManualLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ManualKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Chapter = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderInChapter = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WinnerSeat = table.Column<int>(type: "INTEGER", nullable: false),
                    MovesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XiangqiManualLines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_XiangqiManualLines_ManualKey_Chapter_OrderInChapter",
                table: "XiangqiManualLines",
                columns: new[] { "ManualKey", "Chapter", "OrderInChapter" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "XiangqiManualLines");
        }
    }
}
