using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrambleCoin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVillainTreeAndBotUnlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GameMode",
                table: "Games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VillainId",
                table: "Games",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VillainTreeNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VillainId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VillainName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequiredParentVillainId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnlockedPieceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillainTreeNodes", x => x.Id);
                    table.UniqueConstraint("AK_VillainTreeNodes_VillainId", x => x.VillainId);
                });

            migrationBuilder.CreateTable(
                name: "BotUnlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VillainId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnlockedPieceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DefeatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotUnlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BotUnlocks_VillainTreeNodes_VillainId",
                        column: x => x.VillainId,
                        principalTable: "VillainTreeNodes",
                        principalColumn: "VillainId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BotUnlocks_BotId_VillainId",
                table: "BotUnlocks",
                columns: new[] { "BotId", "VillainId" },
                unique: false);

            migrationBuilder.CreateIndex(
                name: "IX_BotUnlocks_VillainId",
                table: "BotUnlocks",
                column: "VillainId");

            migrationBuilder.CreateIndex(
                name: "IX_VillainTreeNodes_VillainId",
                table: "VillainTreeNodes",
                column: "VillainId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotUnlocks");

            migrationBuilder.DropTable(
                name: "VillainTreeNodes");

            migrationBuilder.DropColumn(
                name: "GameMode",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "VillainId",
                table: "Games");
        }
    }
}
