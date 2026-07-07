using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStageWisePaymentBatchAdditionalDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Account",
                table: "StageWisePaymentBatches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JournalRemark",
                table: "StageWisePaymentBatches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModeOfPayment",
                table: "StageWisePaymentBatches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDate",
                table: "StageWisePaymentBatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostingDate",
                table: "StageWisePaymentBatches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNo",
                table: "StageWisePaymentBatches",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Account",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropColumn(
                name: "JournalRemark",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropColumn(
                name: "ModeOfPayment",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropColumn(
                name: "PaymentDate",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropColumn(
                name: "PostingDate",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropColumn(
                name: "ReferenceNo",
                table: "StageWisePaymentBatches");
        }
    }
}
