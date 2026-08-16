using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 给 <c>Moves</c> 加可空的起点两列 —— 一步棋从此是 <c>(FromRow, FromCol) -> (Row, Col)</c>。
    /// <para>
    /// **这一条 EF 生成得是对的,没有手工改过。** 值得写下来,因为前两条不是:
    /// <c>AddRoomGameKey</c> 生成了 <c>defaultValue: ""</c>(会让所有既有房间不可玩),
    /// <c>DropUserRatingColumns</c> 生成的 <c>Down</c> 会把每个人的分变成 0。
    /// 这次没问题的原因是结构性的:**两列可空、没有缺省值、没有数据要搬**。
    /// 既有的落子类记录本来就没有起点,<c>NULL</c> 就是正确答案,所以不存在"该填什么"这个问题
    /// —— 而前两次的 bug 恰恰都出在 EF 替我们回答了那个问题。
    /// </para>
    /// <para>
    /// 于是它也是可逆的:<c>Down</c> 只丢两列,而那两列在既有数据上全是 <c>NULL</c>。
    /// </para>
    /// </summary>
    public partial class AddMoveOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FromCol",
                table: "Moves",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FromRow",
                table: "Moves",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromCol",
                table: "Moves");

            migrationBuilder.DropColumn(
                name: "FromRow",
                table: "Moves");
        }
    }
}
