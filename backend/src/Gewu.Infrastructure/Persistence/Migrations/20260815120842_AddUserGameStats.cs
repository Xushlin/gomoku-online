using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGameStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserGameStats",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    GamesPlayed = table.Column<int>(type: "INTEGER", nullable: false),
                    Wins = table.Column<int>(type: "INTEGER", nullable: false),
                    Losses = table.Column<int>(type: "INTEGER", nullable: false),
                    Draws = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGameStats", x => new { x.UserId, x.GameKey });
                    table.ForeignKey(
                        name: "FK_UserGameStats_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserGameStats_GameKey_Rating",
                table: "UserGameStats",
                columns: new[] { "GameKey", "Rating" });

            // ---- 回填:既有战绩全部归给五子棋 ----
            //
            // 手写的,不是 EF 生成的。EF 只知道"建一张空表",不知道这张表要接管另一张表的数据。
            // 本仓库在同一个地方被咬过一次:AddRoomGameKey 那次 EF 生成了 defaultValue: ""，
            // 会让每个既有房间的 GameKey 变成空串、解析不出规则、房间全部不可玩。
            //
            // 顺序也是重的:本迁移只**建表 + 回填**,不删 Users 上那五列(expand/contract 的
            // expand 一半)。删列在 contract 那次,那时读者已经切过来了。反过来做——先删列再回填
            // ——数据就没了。
            //
            // RowVersion 用 randomblob(16) 而不是常量:并发令牌若全表同值,第一次并发写就会
            // 出现两行"看起来没被改过",乐观并发保护形同虚设。
            //
            // **回填全部用户,包括 GamesPlayed = 0 的。** 这一条值得说明,因为它与
            // design D4("没下过该棋种的人不上榜")表面冲突:今天的排行榜查询只过滤 !IsBot,
            // 所以一个注册了但没下过棋的人**现在就在榜上**、显示 1200 分。只回填 GamesPlayed > 0
            // 会让这些人从榜上消失 —— 那大概是个改进,但它是一个**产品决定**,不该作为一次迁移的
            // 副作用悄悄发生。本变更的验收判据是"数字一分不差",所以这里选保真。
            // 要不要把零局用户清出排行榜,留给一个能被单独看见的变更。
            migrationBuilder.Sql(
                """
                INSERT INTO UserGameStats
                    (UserId, GameKey, Rating, GamesPlayed, Wins, Losses, Draws, RowVersion)
                SELECT Id, 'gomoku', Rating, GamesPlayed, Wins, Losses, Draws, randomblob(16)
                FROM Users
                WHERE NOT EXISTS (
                    SELECT 1 FROM UserGameStats s
                    WHERE s.UserId = Users.Id AND s.GameKey = 'gomoku');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Users 上那五列本迁移没动过,所以回滚只需丢掉这张表 —— 战绩仍在原处,不会丢。
            // 这正是 expand/contract 拆开做的好处:expand 一半是可逆的。
            migrationBuilder.DropTable(
                name: "UserGameStats");
        }
    }
}
