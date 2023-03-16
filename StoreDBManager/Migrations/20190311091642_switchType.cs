using Microsoft.EntityFrameworkCore.Migrations;

namespace GIPManager.Migrations
{
    public partial class switchType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SwitchType",
                table: "Switches",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SwitchType",
                table: "Switches");
        }
    }
}
