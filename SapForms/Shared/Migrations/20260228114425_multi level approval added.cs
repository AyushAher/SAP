 using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapForm.Migrations
{
    /// <inheritdoc />
    public partial class multilevelapprovaladded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Field",
                table: "ApprovalPolicyApprovers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Operator",
                table: "ApprovalPolicyApprovers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ApprovalPolicyApprovers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Value",
                table: "ApprovalPolicyApprovers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Field",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropColumn(
                name: "Operator",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "ApprovalPolicyApprovers");
        }
    }
}
