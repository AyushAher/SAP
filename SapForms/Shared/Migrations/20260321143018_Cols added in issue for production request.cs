using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapForm.Migrations
{
    /// <inheritdoc />
    public partial class Colsaddedinissueforproductionrequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ItemName",
                table: "IssueForProductionRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ItemNo",
                table: "IssueForProductionRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "IssueForProductionRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "IssueForProductionRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ItemName",
                table: "IssueForProductionRequests");

            migrationBuilder.DropColumn(
                name: "ItemNo",
                table: "IssueForProductionRequests");

            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "IssueForProductionRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "IssueForProductionRequests");
        }
    }
}
