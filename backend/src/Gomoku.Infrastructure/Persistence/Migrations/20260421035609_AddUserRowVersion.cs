using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gomoku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQLite 的 ALTER TABLE ADD COLUMN 不接受"非常量"默认值(禁止 randomblob(16)
            // 直接作 defaultValueSql),所以先用全零 16 字节的常量默认把列加上,
            // 再用一条 UPDATE 为每个现有行生成各自的随机 16 字节 —— 避免全部老行共享同
            // 一个值导致 "任何 SaveChanges 都冲突" 的死锁。
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Users",
                type: "BLOB",
                nullable: false,
                defaultValue: new byte[16]);

            migrationBuilder.Sql("UPDATE Users SET RowVersion = randomblob(16);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Users");
        }
    }
}
