using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 内核从"棋色"改说"座位号"(<c>generalize-match-seats</c>)。
    /// <para>
    /// **EF 生成的版本只有一句改名,而那是不够的 —— 少的那半是静默的。** 两处存量数值要位移:
    /// </para>
    /// <list type="number">
    ///   <item><c>Moves.Stone</c> 存的是 <c>Stone</c> 的底层值(Black=1、White=2),
    ///   而座位号是 0/1。</item>
    ///   <item><c>Games.CurrentTurn</c> 同样存 1/2 —— 而它**连列都没变**(本来就是 int),
    ///   所以生成器对它一个字都没写。</item>
    /// </list>
    /// <para>
    /// 不做位移的后果不是报错,是**错位一位**:每一局进行中的对局会认为轮到另一方,
    /// 每一步历史的出手方会反转 —— 在棋盘上表现为整局颜色翻过来,在结算上表现为赢家错人。
    /// 这正是 <c>AddRoomGameKey</c> 的 <c>defaultValue: ""</c> 和
    /// <c>DropUserRatingColumns</c> 的 <c>defaultValue: 0</c> 同一类:生成器写得出的东西,
    /// 和这次迁移需要的东西,不是一回事。
    /// </para>
    /// </summary>
    public partial class RenameMoveStoneToSeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Stone",
                table: "Moves",
                newName: "Seat");

            // Black=1 → 0 号座位,White=2 → 1 号座位。
            migrationBuilder.Sql("UPDATE Moves SET Seat = Seat - 1;");

            // 同一位移,而这一列的 CLR 类型换了、存储类型没换,所以生成器看不见它。
            migrationBuilder.Sql("UPDATE Games SET CurrentTurn = CurrentTurn - 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 先把数值还原,再改回列名 —— 顺序与 Up 相反。
            migrationBuilder.Sql("UPDATE Games SET CurrentTurn = CurrentTurn + 1;");
            migrationBuilder.Sql("UPDATE Moves SET Seat = Seat + 1;");

            migrationBuilder.RenameColumn(
                name: "Seat",
                table: "Moves",
                newName: "Stone");
        }
    }
}
