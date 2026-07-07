using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDownPaymentStageWisePaymentIdToBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DownPaymentStageWisePaymentId",
                table: "StageWisePaymentBatches",
                type: "integer",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StageWisePaymentBatches_StageWisePayments_DownPaymentStageW~",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropIndex(
                name: "IX_StageWisePaymentBatches_DownPaymentStageWisePaymentId",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropColumn(
                name: "DownPaymentStageWisePaymentId",
                table: "StageWisePaymentBatches");
        }
    }
}
