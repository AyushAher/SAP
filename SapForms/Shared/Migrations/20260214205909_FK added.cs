using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapForm.Migrations
{
    /// <inheritdoc />
    public partial class FKadded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplicationUserId",
                table: "ApprovalPolicyApprovers",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "RequesterUserId",
                table: "ApprovalPolicies",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApplicationUserId",
                table: "ApprovalPolicies",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_RequesterUserId",
                table: "ApprovalRequests",
                column: "RequesterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicyApprovers_ApplicationUserId",
                table: "ApprovalPolicyApprovers",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicyApprovers_ApproverUserId",
                table: "ApprovalPolicyApprovers",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_ApplicationUserId",
                table: "ApprovalPolicies",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_RequesterUserId",
                table: "ApprovalPolicies",
                column: "RequesterUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalPolicies_AspNetUsers_ApplicationUserId",
                table: "ApprovalPolicies",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalPolicies_AspNetUsers_RequesterUserId",
                table: "ApprovalPolicies",
                column: "RequesterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalPolicyApprovers_AspNetUsers_ApplicationUserId",
                table: "ApprovalPolicyApprovers",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalPolicyApprovers_AspNetUsers_ApproverUserId",
                table: "ApprovalPolicyApprovers",
                column: "ApproverUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRequests_AspNetUsers_RequesterUserId",
                table: "ApprovalRequests",
                column: "RequesterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalPolicies_AspNetUsers_ApplicationUserId",
                table: "ApprovalPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalPolicies_AspNetUsers_RequesterUserId",
                table: "ApprovalPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalPolicyApprovers_AspNetUsers_ApplicationUserId",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalPolicyApprovers_AspNetUsers_ApproverUserId",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalRequests_AspNetUsers_RequesterUserId",
                table: "ApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRequests_RequesterUserId",
                table: "ApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicyApprovers_ApplicationUserId",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicyApprovers_ApproverUserId",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicies_ApplicationUserId",
                table: "ApprovalPolicies");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicies_RequesterUserId",
                table: "ApprovalPolicies");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "ApprovalPolicies");

            migrationBuilder.AlterColumn<int>(
                name: "RequesterUserId",
                table: "ApprovalPolicies",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
