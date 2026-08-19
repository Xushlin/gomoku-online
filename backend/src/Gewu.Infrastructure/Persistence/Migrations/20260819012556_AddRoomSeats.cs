using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 房间的两个座位字段变成一张 <c>RoomSeats</c> 表(<c>add-room-seats</c>)。
    /// <para>
    /// **EF 生成的版本有两处错,而它们正是这个仓库已经被咬过两次的那两处:**
    /// </para>
    /// <list type="number">
    ///   <item>它把两列**先删掉再建表** —— 那个顺序下回填是不可能的,存量房间的座位全丢。
    ///   EF 自己也提示了 "may result in the loss of data",而它生成的代码对此什么都没做。</item>
    ///   <item>它的 <c>Down</c> 用 <c>defaultValue: Guid.Empty</c> 把 <c>BlackPlayerId</c> 加回来 ——
    ///   于是每个房间的黑方都变成空 GUID。同 <c>AddRoomGameKey</c> 的 <c>defaultValue: ""</c>
    ///   与 <c>DropUserRatingColumns</c> 的 <c>defaultValue: 0</c>,一模一样的形状。</item>
    /// </list>
    /// <para>
    /// 所以两个方向都手写:<c>Up</c> 先建表、**再回填**、最后才删列;<c>Down</c> 把数据从座位表
    /// 搬回列里,然后才删表。<c>Index</c> 是 SQL 关键字,一律加引号。
    /// </para>
    /// </summary>
    public partial class AddRoomSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomSeats",
                columns: table => new
                {
                    RoomId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomSeats", x => new { x.RoomId, x.Index });
                    table.ForeignKey(
                        name: "FK_RoomSeats_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoomSeats_RoomId_UserId",
                table: "RoomSeats",
                columns: new[] { "RoomId", "UserId" },
                unique: true);

            // 回填必须在删列之前。0 号座位一定有人(建房时就是 host);1 号座位只有
            // 已经有人加入的房间才有,所以那一半带 WHERE。
            migrationBuilder.Sql(
                """
                INSERT INTO RoomSeats (RoomId, "Index", UserId)
                SELECT Id, 0, BlackPlayerId FROM Rooms;
                """);
            migrationBuilder.Sql(
                """
                INSERT INTO RoomSeats (RoomId, "Index", UserId)
                SELECT Id, 1, WhitePlayerId FROM Rooms WHERE WhitePlayerId IS NOT NULL;
                """);

            migrationBuilder.DropColumn(name: "BlackPlayerId", table: "Rooms");
            migrationBuilder.DropColumn(name: "WhitePlayerId", table: "Rooms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 非空列在 SQLite 上必须带默认值才能加回来,所以这个 Guid.Empty 是**过渡值** ——
            // 紧接着的 UPDATE 会把它全部覆盖掉。它与 EF 生成版的区别不在这一行,而在
            // 下面那句 UPDATE 存不存在。
            migrationBuilder.AddColumn<Guid>(
                name: "BlackPlayerId",
                table: "Rooms",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "WhitePlayerId",
                table: "Rooms",
                type: "TEXT",
                nullable: true);

            // 把数据搬回来。若某个房间没有 0 号座位,这条 UPDATE 会把非空列写成 NULL 而失败 ——
            // 那正是想要的:大声坏掉,而不是留下一个空 GUID 的黑方。
            migrationBuilder.Sql(
                """
                UPDATE Rooms SET
                  BlackPlayerId = (SELECT UserId FROM RoomSeats
                                   WHERE RoomSeats.RoomId = Rooms.Id AND RoomSeats."Index" = 0),
                  WhitePlayerId = (SELECT UserId FROM RoomSeats
                                   WHERE RoomSeats.RoomId = Rooms.Id AND RoomSeats."Index" = 1);
                """);

            migrationBuilder.DropTable(name: "RoomSeats");
        }
    }
}
