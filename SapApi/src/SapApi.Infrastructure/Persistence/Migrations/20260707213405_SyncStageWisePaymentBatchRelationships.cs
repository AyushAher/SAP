using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncStageWisePaymentBatchRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StageWisePaymentBatches_StageWisePayments_DownPaymentStageW~",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropIndex(
                name: "IX_StageWisePaymentBatches_DownPaymentStageWisePaymentId",
                table: "StageWisePaymentBatches");

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePaymentBatches_DownPaymentStageWisePaymentId",
                table: "StageWisePaymentBatches",
                column: "DownPaymentStageWisePaymentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StageWisePaymentBatches_StageWisePayments_DownPaymentStageW~",
                table: "StageWisePaymentBatches",
                column: "DownPaymentStageWisePaymentId",
                principalTable: "StageWisePayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StageWisePaymentBatches_StageWisePayments_DownPaymentStageW~",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropIndex(
                name: "IX_StageWisePaymentBatches_DownPaymentStageWisePaymentId",
                table: "StageWisePaymentBatches");

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePaymentBatches_DownPaymentStageWisePaymentId",
                table: "StageWisePaymentBatches",
                column: "DownPaymentStageWisePaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_StageWisePaymentBatches_StageWisePayments_DownPaymentStageW~",
                table: "StageWisePaymentBatches",
                column: "DownPaymentStageWisePaymentId",
                principalTable: "StageWisePayments",
                principalColumn: "Id");
        }
    }
}
