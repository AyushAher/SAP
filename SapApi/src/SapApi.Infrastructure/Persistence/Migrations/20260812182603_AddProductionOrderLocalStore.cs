using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionOrderLocalStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductionOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CompanyDb = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AbsoluteEntry = table.Column<int>(type: "integer", nullable: false),
                    DocumentNumber = table.Column<int>(type: "integer", nullable: true),
                    Series = table.Column<int>(type: "integer", nullable: true),
                    ItemNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProductDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ProductionCategory = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    DrawingNo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    PlannedQuantity = table.Column<double>(type: "double precision", nullable: true),
                    CompletedQuantity = table.Column<double>(type: "double precision", nullable: true),
                    RejectedQuantity = table.Column<double>(type: "double precision", nullable: true),
                    Warehouse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    InventoryUom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UoMEntry = table.Column<int>(type: "integer", nullable: true),
                    CustomerCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Project = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ProjectName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SalesOrderDocEntry = table.Column<int>(type: "integer", nullable: true),
                    SalesOrderDocNum = table.Column<int>(type: "integer", nullable: true),
                    ProductionOrderOrigin = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PostingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    JournalRemarks = table.Column<string>(type: "text", nullable: true),
                    PickRemarks = table.Column<string>(type: "text", nullable: true),
                    Printed = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: true),
                    UserSignature = table.Column<int>(type: "integer", nullable: true),
                    TransactionNumber = table.Column<int>(type: "integer", nullable: true),
                    AttachmentEntry = table.Column<int>(type: "integer", nullable: true),
                    RoutingDateCalculation = table.Column<string>(type: "text", nullable: true),
                    UpdateAllocation = table.Column<string>(type: "text", nullable: true),
                    DistributionRule = table.Column<string>(type: "text", nullable: true),
                    DistributionRule2 = table.Column<string>(type: "text", nullable: true),
                    DistributionRule3 = table.Column<string>(type: "text", nullable: true),
                    DistributionRule4 = table.Column<string>(type: "text", nullable: true),
                    DistributionRule5 = table.Column<string>(type: "text", nullable: true),
                    SyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionOrderSyncLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CompanyDb = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AbsoluteEntry = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AddedCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedCount = table.Column<int>(type: "integer", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderSyncLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionOrderSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CompanyDb = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncedCount = table.Column<int>(type: "integer", nullable: true),
                    LastSyncMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HangfireJobId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAbsoluteEntry = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderSyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionOrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ProductionOrderId = table.Column<int>(type: "integer", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ItemNo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ItemType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LineText = table.Column<string>(type: "text", nullable: true),
                    BaseQuantity = table.Column<double>(type: "double precision", nullable: true),
                    PlannedQuantity = table.Column<double>(type: "double precision", nullable: true),
                    IssuedQuantity = table.Column<double>(type: "double precision", nullable: true),
                    AdditionalQuantity = table.Column<double>(type: "double precision", nullable: true),
                    ProductionOrderIssueType = table.Column<string>(type: "text", nullable: true),
                    Warehouse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    VisualOrder = table.Column<int>(type: "integer", nullable: true),
                    LocationCode = table.Column<int>(type: "integer", nullable: true),
                    Project = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UoMEntry = table.Column<int>(type: "integer", nullable: true),
                    UoMCode = table.Column<int>(type: "integer", nullable: true),
                    WipAccount = table.Column<string>(type: "text", nullable: true),
                    StageId = table.Column<int>(type: "integer", nullable: true),
                    RequiredDays = table.Column<double>(type: "double precision", nullable: true),
                    ResourceAllocation = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DistributionRule = table.Column<string>(type: "text", nullable: true),
                    DistributionRule2 = table.Column<string>(type: "text", nullable: true),
                    DistributionRule3 = table.Column<string>(type: "text", nullable: true),
                    DistributionRule4 = table.Column<string>(type: "text", nullable: true),
                    DistributionRule5 = table.Column<string>(type: "text", nullable: true),
                    FreeText = table.Column<string>(type: "text", nullable: true),
                    DocNum = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrderLines_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderLines_ItemNo",
                table: "ProductionOrderLines",
                column: "ItemNo");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderLines_ProductionOrderId_LineNumber",
                table: "ProductionOrderLines",
                columns: new[] { "ProductionOrderId", "LineNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CompanyDb_AbsoluteEntry",
                table: "ProductionOrders",
                columns: new[] { "CompanyDb", "AbsoluteEntry" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CompanyDb_CustomerCode",
                table: "ProductionOrders",
                columns: new[] { "CompanyDb", "CustomerCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CompanyDb_DocumentNumber",
                table: "ProductionOrders",
                columns: new[] { "CompanyDb", "DocumentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CompanyDb_ItemNo",
                table: "ProductionOrders",
                columns: new[] { "CompanyDb", "ItemNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CompanyDb_Project",
                table: "ProductionOrders",
                columns: new[] { "CompanyDb", "Project" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CompanyDb_SalesOrderDocNum",
                table: "ProductionOrders",
                columns: new[] { "CompanyDb", "SalesOrderDocNum" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CompanyDb_Status",
                table: "ProductionOrders",
                columns: new[] { "CompanyDb", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderSyncLogs_CompanyDb_AbsoluteEntry",
                table: "ProductionOrderSyncLogs",
                columns: new[] { "CompanyDb", "AbsoluteEntry" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderSyncLogs_CompanyDb_CreatedOn",
                table: "ProductionOrderSyncLogs",
                columns: new[] { "CompanyDb", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderSyncStates_CompanyDb",
                table: "ProductionOrderSyncStates",
                column: "CompanyDb",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionOrderLines");

            migrationBuilder.DropTable(
                name: "ProductionOrderSyncLogs");

            migrationBuilder.DropTable(
                name: "ProductionOrderSyncStates");

            migrationBuilder.DropTable(
                name: "ProductionOrders");
        }
    }
}
