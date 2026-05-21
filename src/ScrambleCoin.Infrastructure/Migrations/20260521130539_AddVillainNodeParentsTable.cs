using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScrambleCoin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVillainNodeParentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiredParentVillainId",
                table: "VillainTreeNodes");

            migrationBuilder.CreateTable(
                name: "VillainNodeParents",
                columns: table => new
                {
                    ParentVillainId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChildVillainId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillainNodeParents", x => new { x.ChildVillainId, x.ParentVillainId });
                    table.ForeignKey(
                        name: "FK_VillainNodeParents_VillainTreeNodes_ChildVillainId",
                        column: x => x.ChildVillainId,
                        principalTable: "VillainTreeNodes",
                        principalColumn: "VillainId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VillainNodeParents");

            migrationBuilder.AddColumn<string>(
                name: "RequiredParentVillainId",
                table: "VillainTreeNodes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
