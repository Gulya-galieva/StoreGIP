using Microsoft.EntityFrameworkCore.Migrations;

namespace GIPManager.Migrations
{
    public partial class _2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CuratorId",
                table: "MounterReportUgesALs",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CuratorId",
                table: "MounterReportUgesALs");
        }
    }
}
