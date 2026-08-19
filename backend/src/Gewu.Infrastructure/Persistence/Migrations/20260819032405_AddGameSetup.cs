using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 给 <c>Games</c> 加一个可空的 <c>Setup</c> 列 —— 本局的服务端侧对局设置。
    /// <para>
    /// <b>这是本仓库第一个可以直接采用 EF 生成结果的迁移</b>,而这句话是**核对过**的,不是
    /// 默认它对:纯加宽,没有回填、没有值重映射、没有删列,<c>AddColumn</c> 也没有
    /// <c>defaultValue</c>。前面四次各自错在不同的地方(<c>defaultValue: ""</c> /
    /// <c>defaultValue: 0</c> / drop-before-create / 值位移隐形),所以核对本身仍然是必要的
    /// 那一步 —— 变的只是这一次的结论。
    /// </para>
    /// <para>
    /// <b><c>Down</c> 为什么不需要守卫。</b> 收窄一列而底下有装不进去的数据时通常必须拒绝
    /// (见 <c>AddMoveTextPayload</c> 与 <c>RemapGameResultValues</c>)。这里不同:<c>Setup</c>
    /// 的**唯一**读者是需要它的那个棋种的规则,而回滚到本迁移之前意味着那个棋种在这个构建里
    /// 还不存在,所以不可能有非 <c>NULL</c> 的行需要保护。**这个理由写在这里,是因为下一个人
    /// 只会看到"这个 Down 没有守卫",而无从知道那是核对过的结论还是漏掉的。**
    /// </para>
    /// </summary>
    public partial class AddGameSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Setup",
                table: "Games",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Setup",
                table: "Games");
        }
    }
}
