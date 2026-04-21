using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gomoku.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHardBotAccount : Migration
    {
        // 与 Gomoku.Application.Abstractions.BotAccountIds.Hard 完全一致。
        // 迁移 + BotAccountIds 是整个代码库中**唯一**允许出现此字面量的地方。
        private static readonly Guid HardBotId = Guid.Parse("00000000-0000-0000-0000-0000000000ad");

        // 与 Gomoku.Domain.Users.User.BotPasswordHashMarker 同值。
        private const string BotPasswordHashMarker = "__BOT_NO_LOGIN__";

        // 固定时间戳(UTC),保证 migration 可重放。
        private static readonly DateTime BotCreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // 固定 16 字节 RowVersion —— 从"hard"的 ASCII 派生一个 marker 值,运行时后续 RecordGameResult
        // 会把它替换为随机 Guid 字节,所以这里只需保证与其它 bot 不同即可。
        private static readonly byte[] HardBotRowVersion = new byte[]
        {
            0x48, 0x41, 0x52, 0x44, // "HARD"
            0x42, 0x4F, 0x54, 0x00, // "BOT\0"
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x01,
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // AddUserRowVersion migration 已经在 Users 表加了 RowVersion 列;
            // 所以此次 Insert 必须显式提供 RowVersion 值(列为 NOT NULL)。
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
                    "RowVersion",
                },
                values: new object[]
                {
                    HardBotId,
                    "hard@bot.gomoku.local",
                    "AI_Hard",
                    BotPasswordHashMarker,
                    1200, 0, 0, 0, 0,
                    true,   // IsActive
                    true,   // IsBot
                    BotCreatedAt,
                    HardBotRowVersion,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: HardBotId);
        }
    }
}
