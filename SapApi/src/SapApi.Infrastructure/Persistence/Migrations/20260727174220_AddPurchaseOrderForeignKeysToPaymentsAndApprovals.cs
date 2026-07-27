using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderForeignKeysToPaymentsAndApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PurchaseOrderId",
                table: "StageWisePayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseOrderId",
                table: "StageWisePaymentBatches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseOrderId",
                table: "ApprovalRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePayments_PurchaseOrderId",
                table: "StageWisePayments",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePaymentBatches_PurchaseOrderId",
                table: "StageWisePaymentBatches",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRequests_PurchaseOrderId",
                table: "ApprovalRequests",
                column: "PurchaseOrderId");

            // Backfill FKs from existing SAP identity columns where local POs already exist.
            migrationBuilder.Sql("""
                UPDATE "StageWisePaymentBatches" AS b
                SET "PurchaseOrderId" = po."Id"
                FROM "PurchaseOrders" AS po
                WHERE b."PurchaseOrderId" IS NULL
                  AND po."CompanyDb" = b."CompanyDb"
                  AND po."DocEntry" = b."PoDocEntry";

                UPDATE "StageWisePayments" AS p
                SET "PurchaseOrderId" = po."Id"
                FROM "PurchaseOrders" AS po
                WHERE p."PurchaseOrderId" IS NULL
                  AND p."DocNumber" IS NOT NULL
                  AND po."CompanyDb" = p."CompanyDb"
                  AND po."DocNum" = p."DocNumber";
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRequests_PurchaseOrders_PurchaseOrderId",
                table: "ApprovalRequests",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StageWisePaymentBatches_PurchaseOrders_PurchaseOrderId",
                table: "StageWisePaymentBatches",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StageWisePayments_PurchaseOrders_PurchaseOrderId",
                table: "StageWisePayments",
                column: "PurchaseOrderId",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalRequests_PurchaseOrders_PurchaseOrderId",
                table: "ApprovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_StageWisePaymentBatches_PurchaseOrders_PurchaseOrderId",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_StageWisePayments_PurchaseOrders_PurchaseOrderId",
                table: "StageWisePayments");

            migrationBuilder.DropIndex(
                name: "IX_StageWisePayments_PurchaseOrderId",
                table: "StageWisePayments");

            migrationBuilder.DropIndex(
                name: "IX_StageWisePaymentBatches_PurchaseOrderId",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalRequests_PurchaseOrderId",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                table: "StageWisePayments");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderId",
                table: "ApprovalRequests");
        }
    }
}
