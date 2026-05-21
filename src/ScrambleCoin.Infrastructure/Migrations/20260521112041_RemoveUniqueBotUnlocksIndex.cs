using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrambleCoin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueBotUnlocksIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BotUnlocks_BotId_VillainId",
                table: "BotUnlocks");

            migrationBuilder.CreateIndex(
                name: "IX_BotUnlocks_BotId_VillainId",
                table: "BotUnlocks",
                columns: new[] { "BotId", "VillainId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BotUnlocks_BotId_VillainId",
                table: "BotUnlocks");

            migrationBuilder.CreateIndex(
                name: "IX_BotUnlocks_BotId_VillainId",
                table: "BotUnlocks",
                columns: new[] { "BotId", "VillainId" },
                unique: true);
        }
    }
}
