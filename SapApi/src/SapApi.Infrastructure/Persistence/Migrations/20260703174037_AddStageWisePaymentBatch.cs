using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStageWisePaymentBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StageWisePaymentBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyDb = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PoDocEntry = table.Column<int>(type: "integer", nullable: false),
                    DocNumber = table.Column<int>(type: "integer", nullable: true),
                    StageWisePaymentId = table.Column<int>(type: "integer", nullable: true),
                    ApprovalRequestId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageWisePaymentBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageWisePaymentBatches_StageWisePayments_StageWisePaymentId",
                        column: x => x.StageWisePaymentId,
                        principalTable: "StageWisePayments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StageWisePaymentBatchLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    ApInvoiceDocEntry = table.Column<string>(type: "text", nullable: true),
                    Bank = table.Column<string>(type: "text", nullable: true),
                    WtCode = table.Column<string>(type: "text", nullable: true),
                    Amount = table.Column<double>(type: "double precision", nullable: false),
                    BalanceDue = table.Column<double>(type: "double precision", nullable: true),
                    Payable = table.Column<double>(type: "double precision", nullable: true),
                    LineOrder = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageWisePaymentBatchLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageWisePaymentBatchLines_StageWisePaymentBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "StageWisePaymentBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StageWisePaymentBatchLinePaymentTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LineId = table.Column<int>(type: "integer", nullable: false),
                    PaymentTermsType = table.Column<int>(type: "integer", nullable: false),
                    PaymentTermDesc = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageWisePaymentBatchLinePaymentTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageWisePaymentBatchLinePaymentTerms_StageWisePaymentBatch~",
                        column: x => x.LineId,
                        principalTable: "StageWisePaymentBatchLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePaymentBatches_ApprovalRequestId",
                table: "StageWisePaymentBatches",
                column: "ApprovalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePaymentBatches_CompanyDb_PoDocEntry",
                table: "StageWisePaymentBatches",
                columns: new[] { "CompanyDb", "PoDocEntry" });

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePaymentBatches_StageWisePaymentId",
                table: "StageWisePaymentBatches",
                column: "StageWisePaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePaymentBatchLinePaymentTerms_LineId_PaymentTermsTy~",
                table: "StageWisePaymentBatchLinePaymentTerms",
                columns: new[] { "LineId", "PaymentTermsType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePaymentBatchLines_BatchId",
                table: "StageWisePaymentBatchLines",
                column: "BatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StageWisePaymentBatchLinePaymentTerms");

            migrationBuilder.DropTable(
                name: "StageWisePaymentBatchLines");

            migrationBuilder.DropTable(
                name: "StageWisePaymentBatches");
        }
    }
}
