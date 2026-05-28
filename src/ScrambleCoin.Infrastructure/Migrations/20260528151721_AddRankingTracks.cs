using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrambleCoin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRankingTracks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RankingTracks",
                columns: table => new
                {
                    BotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BotName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    Wins = table.Column<int>(type: "int", nullable: false),
                    Draws = table.Column<int>(type: "int", nullable: false),
                    Losses = table.Column<int>(type: "int", nullable: false),
                    GamesPlayed = table.Column<int>(type: "int", nullable: false),
                    MilestonesHit = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingTracks", x => x.BotId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RankingTracks");
        }
    }
}
