using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrambleCoin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MaxParticipants = table.Column<int>(type: "int", nullable: false),
                    TopN = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WinnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Participants = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GroupMatches = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KnockoutMatches = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
