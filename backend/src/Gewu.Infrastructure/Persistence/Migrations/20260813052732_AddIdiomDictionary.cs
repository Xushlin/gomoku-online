using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdiomDictionary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Idioms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Word = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Pinyin = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", nullable: false),
                    Derivation = table.Column<string>(type: "TEXT", nullable: false),
                    Example = table.Column<string>(type: "TEXT", nullable: false),
                    CharCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MinCharFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    TierOverride = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Idioms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdiomChars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IdiomId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Char = table.Column<char>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdiomChars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdiomChars_Idioms_IdiomId",
                        column: x => x.IdiomId,
                        principalTable: "Idioms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdiomChars_Char_Position",
                table: "IdiomChars",
                columns: new[] { "Char", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_IdiomChars_IdiomId",
                table: "IdiomChars",
                column: "IdiomId");

            migrationBuilder.CreateIndex(
                name: "IX_IdiomChars_Position_Char",
                table: "IdiomChars",
                columns: new[] { "Position", "Char" });

            migrationBuilder.CreateIndex(
                name: "IX_Idioms_Tier",
                table: "Idioms",
                column: "Tier");

            migrationBuilder.CreateIndex(
                name: "IX_Idioms_Word",
                table: "Idioms",
                column: "Word",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdiomChars");

            migrationBuilder.DropTable(
                name: "Idioms");
        }
    }
}
