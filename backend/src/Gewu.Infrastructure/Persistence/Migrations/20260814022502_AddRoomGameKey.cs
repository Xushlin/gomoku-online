using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 给 <c>Rooms</c> 加上棋种键。
    /// <para>
    /// 默认值必须是 <c>'gomoku'</c> 而不是 EF 生成的空串:空键在规则注册表里解析不出东西,
    /// 迁移前建立的房间会因此变成 404、再也玩不了。<c>Up</c> 里额外显式回填一次,
    /// 不依赖默认值对既有行生效。
    /// </para>
    /// </summary>
    public partial class AddRoomGameKey : Migration
    {
        private const string Gomoku = "gomoku";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GameKey",
                table: "Rooms",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: Gomoku);

            // 显式回填:迁移之前存在的每一间房都是五子棋。
            migrationBuilder.Sql($"UPDATE Rooms SET GameKey = '{Gomoku}' WHERE GameKey IS NULL OR GameKey = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameKey",
                table: "Rooms");
        }
    }
}
