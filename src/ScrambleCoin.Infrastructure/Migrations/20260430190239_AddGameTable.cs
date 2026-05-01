using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrambleCoin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerOne = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerTwo = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TurnNumber = table.Column<int>(type: "int", nullable: false),
                    CurrentPhase = table.Column<int>(type: "int", nullable: true),
                    MovePhaseActivePlayer = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Scores = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PiecesOnBoard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlacePhaseDone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MovedPieceIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LineupPlayerOne = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LineupPlayerTwo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BoardState = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
