using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 房间多一列:建房时**选定**的开局设置(目前只有象棋残局用它)。
    /// <para>
    /// <b>这一次 EF 生成的是对的,而那不是运气 —— 是因为这里没有数据要搬。</b> 新列可空,
    /// 既有的房间全都不是选定式棋种,<c>null</c> 对它们**就是正确答案**。前五次付账的
    /// 场合都有一个共同点:新列有一个「看起来合理」的非空缺省,或者旧列的数值要换一套含义。
    /// 这一列两样都没有。
    /// </para>
    /// <para>
    /// <c>Down</c> 丢掉这一列,而那**不是**这次要防的东西:回滚过这一版意味着代码也回滚,
    /// 那时 <c>xiangqi-endgame</c> 不在规则注册表里,那些房间本来就打不开了 —— 列在不在,
    /// 结果一样。所以它不进「回滚欠账」那张表。
    /// </para>
    /// </summary>
    public partial class AddRoomChosenSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChosenSetup",
                table: "Rooms",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChosenSetup",
                table: "Rooms");
        }
    }
}
