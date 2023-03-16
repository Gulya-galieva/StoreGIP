using Microsoft.EntityFrameworkCore.Migrations;

namespace GIPManager.Migrations
{
    public partial class flagsReplace : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReplace",
                table: "RegPointFlags",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReplace",
                table: "RegPointFlags");
        }
    }
}
