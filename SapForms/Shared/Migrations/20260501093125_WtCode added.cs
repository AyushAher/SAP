using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapForm.Migrations
{
    /// <inheritdoc />
    public partial class WtCodeadded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WtCode",
                table: "StageWisePayments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WtCode",
                table: "StageWisePayments");
        }
    }
}
