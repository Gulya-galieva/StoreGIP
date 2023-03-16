using Microsoft.EntityFrameworkCore.Migrations;

namespace GIPManager.Migrations
{
    public partial class Unmountreport_netRegionId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NetRegionId",
                table: "UnmountReports",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NetRegionId",
                table: "UnmountReports");
        }
    }
}
