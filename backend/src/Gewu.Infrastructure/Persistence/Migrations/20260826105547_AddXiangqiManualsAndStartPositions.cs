using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 古谱:加谱表、起始局面、先走方,并把「获胜座位」换成四态的谱评。
    /// <para>
    /// <b>EF 生成的版本在这里是错的,而且错得静默 —— 两处。</b>
    /// </para>
    /// <list type="number">
    /// <item><description>它把 <c>WinnerSeat</c> <b>改名</b>成 <c>Verdict</c>,于是数值原样留下:
    /// 而 <c>ManualVerdict.Unrecorded</c> 是 <b>0</b>,<c>RedBetter</c> 是 1。既有 31 行里
    /// 「红胜(座位 0)」会全部变成<b>谱未标注</b>,「黑胜(1)」变成<b>红优</b> ——
    /// 一次静默的数据损坏,而库里看起来一切正常。</description></item>
    /// <item><description>它给 <c>StartPosition</c> 填 <c>""</c>,而领域要求恰好 90 字符。
    /// 一个 0 长度的盘面串会让 <c>row * 9 + col</c> 直接越界,或者更糟 —— 画出一个空棋盘。</description></item>
    /// </list>
    /// <para>
    /// 所以数值搬运是手写的。这是这个仓库第五次为 EF 生成的数据搬运付账,前四次都在
    /// <c>Down</c> 上(<c>AddColumn(defaultValue: 0)</c> 恢复出貌似合理的垃圾);
    /// <b>这一次在 <c>Up</c> 上</b>,而 <c>Up</c> 出错是上线才发现的那一类。
    /// </para>
    /// <para>
    /// <c>Down</c> 丢数据是<b>安全</b>的:每一行都能从仓库里提交的产物完整复现
    /// (<c>data/manuals/*.json</c>),播种器还会重跑一遍校验。而和棋 / 谱未标注在旧列里
    /// <b>没有表示</b>,所以那些行只能删 —— 与 <c>AddGameSetup</c> 那条延期项的区别是
    /// 「数据从哪来」,不是「drop 了什么」。
    /// </para>
    /// </summary>
    public partial class AddXiangqiManualsAndStartPositions : Migration
    {
        /// <summary>标准开局的盘面串 —— 与 <c>XiangqiManualSeeder.StandardBoard</c> 同一个字符串。</summary>
        private const string StandardBoard =
            "rnbakabnr..........c.....c.p.p.p.p.p..................P.P.P.P.P.C.....C..........RNBAKABNR";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WinnerSeat",
                table: "XiangqiManualLines",
                newName: "Verdict");

            migrationBuilder.AddColumn<int>(
                name: "FirstSeat",
                table: "XiangqiManualLines",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StartPosition",
                table: "XiangqiManualLines",
                type: "TEXT",
                fixedLength: true,
                maxLength: 90,
                nullable: false,
                defaultValue: "");

            // ---- 手写的数据搬运 ----
            //
            // 既有的行全部来自《梅花谱》,而它们的起始局面全是标准开局、先走方全是红。
            // 先补起始局面(FirstSeat 的默认 0 已经是对的)。
            migrationBuilder.Sql(
                $"UPDATE XiangqiManualLines SET StartPosition = '{StandardBoard}' " +
                "WHERE StartPosition = '' OR StartPosition IS NULL;");

            // 再把座位号重映射成谱评。**顺序要紧**:先 1 -> 2,再 0 -> 1,否则第二步会把
            // 第一步刚写出来的 1 又搬走一次 —— 那种错误跑完之后库里一切正常。
            migrationBuilder.Sql("UPDATE XiangqiManualLines SET Verdict = 2 WHERE Verdict = 1;");
            migrationBuilder.Sql("UPDATE XiangqiManualLines SET Verdict = 1 WHERE Verdict = 0;");

            migrationBuilder.CreateTable(
                name: "XiangqiManuals",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Grouped = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XiangqiManuals", x => x.Key);
                });

            // 《梅花谱》的身份行 —— 它此前没有表可以待。其余六辑由播种器写入。
            migrationBuilder.Sql(
                "INSERT INTO XiangqiManuals (Key, Name, Grouped) " +
                "SELECT 'meihuapu', '梅花谱', 1 " +
                "WHERE EXISTS (SELECT 1 FROM XiangqiManualLines WHERE ManualKey = 'meihuapu') " +
                "AND NOT EXISTS (SELECT 1 FROM XiangqiManuals WHERE Key = 'meihuapu');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 和棋与谱未标注在旧列里没有表示 —— 那些行只能删,而它们能从产物完整复现。
            migrationBuilder.Sql("DELETE FROM XiangqiManualLines WHERE Verdict IN (0, 3);");

            // 反向重映射,顺序同样要紧:先 1 -> 0,再 2 -> 1。
            migrationBuilder.Sql("UPDATE XiangqiManualLines SET Verdict = 0 WHERE Verdict = 1;");
            migrationBuilder.Sql("UPDATE XiangqiManualLines SET Verdict = 1 WHERE Verdict = 2;");

            migrationBuilder.DropTable(
                name: "XiangqiManuals");

            migrationBuilder.DropColumn(
                name: "FirstSeat",
                table: "XiangqiManualLines");

            migrationBuilder.DropColumn(
                name: "StartPosition",
                table: "XiangqiManualLines");

            migrationBuilder.RenameColumn(
                name: "Verdict",
                table: "XiangqiManualLines",
                newName: "WinnerSeat");
        }
    }
}
