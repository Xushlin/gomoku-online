using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gomoku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBotSupport : Migration
    {
        // 这两个 Guid 必须与 Gomoku.Application.Abstractions.BotAccountIds 完全一致。
        // 迁移 + BotAccountIds 是整个代码库中**唯一**允许出现这两个字面量的地方。
        private static readonly Guid EasyBotId = Guid.Parse("00000000-0000-0000-0000-00000000ea51");
        private static readonly Guid MediumBotId = Guid.Parse("00000000-0000-0000-0000-0000000bed10");

        // 固定常量,与 Gomoku.Domain.Users.User.BotPasswordHashMarker 同值。
        // 任何 PasswordHasher.Verify 对此值都会返回 Failed。
        private const string BotPasswordHashMarker = "__BOT_NO_LOGIN__";

        // 迁移内固定时间戳(UTC),保证可重放性。
        private static readonly DateTime BotCreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBot",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Seed 两个机器人账号。列名与 Users 表当前 schema 对齐(Email / Username
            // 经 OwnsOne 映射到扁平列;Id 经 UserIdConverter 存为 Guid)。
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[]
                {
                    "Id",
                    "Email",
                    "Username",
                    "PasswordHash",
                    "Rating",
                    "GamesPlayed",
                    "Wins",
                    "Losses",
                    "Draws",
                    "IsActive",
                    "IsBot",
                    "CreatedAt",
                },
                values: new object[,]
                {
                    {
                        EasyBotId,
                        "easy@bot.gomoku.local",
                        "AI_Easy",
                        BotPasswordHashMarker,
                        1200, 0, 0, 0, 0,
                        true,  // IsActive
                        true,  // IsBot
                        BotCreatedAt,
                    },
                    {
                        MediumBotId,
                        "medium@bot.gomoku.local",
                        "AI_Medium",
                        BotPasswordHashMarker,
                        1200, 0, 0, 0, 0,
                        true,
                        true,
                        BotCreatedAt,
                    },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: EasyBotId);
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: MediumBotId);

            migrationBuilder.DropColumn(
                name: "IsBot",
                table: "Users");
        }
    }
}
