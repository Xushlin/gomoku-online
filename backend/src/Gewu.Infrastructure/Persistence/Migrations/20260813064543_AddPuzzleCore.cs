using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gewu.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPuzzleCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PuzzleLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LevelIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    LayoutJson = table.Column<string>(type: "TEXT", nullable: false),
                    SolutionJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleLevels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PuzzleAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PuzzleLevelId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HintsUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    Mistakes = table.Column<int>(type: "INTEGER", nullable: false),
                    Stars = table.Column<int>(type: "INTEGER", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PuzzleAttempts_PuzzleLevels_PuzzleLevelId",
                        column: x => x.PuzzleLevelId,
                        principalTable: "PuzzleLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PuzzleLevelProgress",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PuzzleLevelId = table.Column<int>(type: "INTEGER", nullable: false),
                    BestStars = table.Column<int>(type: "INTEGER", nullable: false),
                    BestDurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PuzzleLevelProgress", x => new { x.UserId, x.PuzzleLevelId });
                    table.ForeignKey(
                        name: "FK_PuzzleLevelProgress_PuzzleLevels_PuzzleLevelId",
                        column: x => x.PuzzleLevelId,
                        principalTable: "PuzzleLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleAttempts_Id_UserId",
                table: "PuzzleAttempts",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleAttempts_PuzzleLevelId",
                table: "PuzzleAttempts",
                column: "PuzzleLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleAttempts_UserId_PuzzleLevelId",
                table: "PuzzleAttempts",
                columns: new[] { "UserId", "PuzzleLevelId" });

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleLevelProgress_PuzzleLevelId",
                table: "PuzzleLevelProgress",
                column: "PuzzleLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_PuzzleLevels_GameKey_LevelIndex",
                table: "PuzzleLevels",
                columns: new[] { "GameKey", "LevelIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PuzzleAttempts");

            migrationBuilder.DropTable(
                name: "PuzzleLevelProgress");

            migrationBuilder.DropTable(
                name: "PuzzleLevels");
        }
    }
}
