using Microsoft.EntityFrameworkCore.Migrations;

namespace GIPManager.Migrations
{
    public partial class USPDReport_CuratorId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CuratorId",
                table: "USPDReports",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CuratorId",
                table: "USPDReports");
        }
    }
}
