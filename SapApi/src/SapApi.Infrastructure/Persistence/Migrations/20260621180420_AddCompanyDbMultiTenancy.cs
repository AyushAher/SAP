using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyDbMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CacheEntries_ExpiresAtUtc",
                table: "CacheEntries");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRequests_OverallStatus",
                table: "ApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicies_DocumentType",
                table: "ApprovalPolicies");

            migrationBuilder.AddColumn<string>(
                name: "CompanyDb",
                table: "StageWisePayments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "PBBPL_UAT");

            migrationBuilder.AddColumn<string>(
                name: "CompanyDb",
                table: "ReceiptFromProductionRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "PBBPL_UAT");

            migrationBuilder.AddColumn<string>(
                name: "CompanyDb",
                table: "IssueForProductionRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "PBBPL_UAT");

            migrationBuilder.AddColumn<string>(
                name: "CompanyDb",
                table: "CacheEntries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "PBBPL_UAT");

            migrationBuilder.AddColumn<string>(
                name: "CompanyDb",
                table: "ApprovalRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "PBBPL_UAT");

            migrationBuilder.AddColumn<string>(
                name: "CompanyDb",
                table: "ApprovalPolicies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "PBBPL_UAT");

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePayments_CompanyDb_DocNumber",
                table: "StageWisePayments",
                columns: new[] { "CompanyDb", "DocNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptFromProductionRequests_CompanyDb",
                table: "ReceiptFromProductionRequests",
                column: "CompanyDb");

            migrationBuilder.CreateIndex(
                name: "IX_IssueForProductionRequests_CompanyDb",
                table: "IssueForProductionRequests",
                column: "CompanyDb");

            migrationBuilder.CreateIndex(
                name: "IX_CacheEntries_CompanyDb_ExpiresAtUtc",
                table: "CacheEntries",
                columns: new[] { "CompanyDb", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_CompanyDb_OverallStatus",
                table: "ApprovalRequests",
                columns: new[] { "CompanyDb", "OverallStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_CompanyDb_DocumentType",
                table: "ApprovalPolicies",
                columns: new[] { "CompanyDb", "DocumentType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StageWisePayments_CompanyDb_DocNumber",
                table: "StageWisePayments");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptFromProductionRequests_CompanyDb",
                table: "ReceiptFromProductionRequests");

            migrationBuilder.DropIndex(
                name: "IX_IssueForProductionRequests_CompanyDb",
                table: "IssueForProductionRequests");

            migrationBuilder.DropIndex(
                name: "IX_CacheEntries_CompanyDb_ExpiresAtUtc",
                table: "CacheEntries");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRequests_CompanyDb_OverallStatus",
                table: "ApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicies_CompanyDb_DocumentType",
                table: "ApprovalPolicies");

            migrationBuilder.DropColumn(
                name: "CompanyDb",
                table: "StageWisePayments");

            migrationBuilder.DropColumn(
                name: "CompanyDb",
                table: "ReceiptFromProductionRequests");

            migrationBuilder.DropColumn(
                name: "CompanyDb",
                table: "IssueForProductionRequests");

            migrationBuilder.DropColumn(
                name: "CompanyDb",
                table: "CacheEntries");

            migrationBuilder.DropColumn(
                name: "CompanyDb",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "CompanyDb",
                table: "ApprovalPolicies");

            migrationBuilder.CreateIndex(
                name: "IX_CacheEntries_ExpiresAtUtc",
                table: "CacheEntries",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_OverallStatus",
                table: "ApprovalRequests",
                column: "OverallStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_DocumentType",
                table: "ApprovalPolicies",
                column: "DocumentType");
        }
    }
}
