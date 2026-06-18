using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapForm.Migrations
{
    /// <inheritdoc />
    public partial class FixCascadePaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalPolicyApprovers_ApprovalPolicies_PolicyId",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropColumn(
                name: "Field",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropColumn(
                name: "Operator",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropColumn(
                name: "MinApprovalRequired",
                table: "ApprovalPolicies");

            migrationBuilder.RenameColumn(
                name: "PolicyId",
                table: "ApprovalPolicyApprovers",
                newName: "ApprovalPolicyId");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalPolicyApprovers_PolicyId",
                table: "ApprovalPolicyApprovers",
                newName: "IX_ApprovalPolicyApprovers_ApprovalPolicyId");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "UserApprovals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ApprovalPolicyRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApprovalPolicyId = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalPolicyRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalPolicyRules_ApprovalPolicies_ApprovalPolicyId",
                        column: x => x.ApprovalPolicyId,
                        principalTable: "ApprovalPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicyRules_ApprovalPolicyId",
                table: "ApprovalPolicyRules",
                column: "ApprovalPolicyId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalPolicyApprovers_ApprovalPolicies_ApprovalPolicyId",
                table: "ApprovalPolicyApprovers",
                column: "ApprovalPolicyId",
                principalTable: "ApprovalPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserApprovals_AspNetUsers_UserId",
                table: "UserApprovals",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalPolicyApprovers_ApprovalPolicies_ApprovalPolicyId",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropForeignKey(
                name: "FK_UserApprovals_AspNetUsers_UserId",
                table: "UserApprovals");

            migrationBuilder.DropTable(
                name: "ApprovalPolicyRules");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "UserApprovals");

            migrationBuilder.RenameColumn(
                name: "ApprovalPolicyId",
                table: "ApprovalPolicyApprovers",
                newName: "PolicyId");

            migrationBuilder.RenameIndex(
                name: "IX_ApprovalPolicyApprovers_ApprovalPolicyId",
                table: "ApprovalPolicyApprovers",
                newName: "IX_ApprovalPolicyApprovers_PolicyId");

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

            migrationBuilder.AddColumn<string>(
                name: "Value",
                table: "ApprovalPolicyApprovers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinApprovalRequired",
                table: "ApprovalPolicies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalPolicyApprovers_ApprovalPolicies_PolicyId",
                table: "ApprovalPolicyApprovers",
                column: "PolicyId",
                principalTable: "ApprovalPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
