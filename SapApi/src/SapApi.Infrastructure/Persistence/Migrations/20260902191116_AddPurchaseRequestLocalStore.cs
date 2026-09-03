using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseRequestLocalStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PurchaseRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CompanyDb = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocEntry = table.Column<int>(type: "integer", nullable: false),
                    DocNum = table.Column<int>(type: "integer", nullable: true),
                    DocType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Project = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CardCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CardName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DocTotal = table.Column<double>(type: "double precision", nullable: true),
                    VatSum = table.Column<double>(type: "double precision", nullable: true),
                    NumAtCard = table.Column<string>(type: "text", nullable: true),
                    DocumentStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    DocCurrency = table.Column<string>(type: "text", nullable: true),
                    DocRate = table.Column<double>(type: "double precision", nullable: true),
                    JournalMemo = table.Column<string>(type: "text", nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    Requester = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RequesterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequesterEmail = table.Column<string>(type: "text", nullable: true),
                    ReqCode = table.Column<string>(type: "text", nullable: true),
                    ReqType = table.Column<int>(type: "integer", nullable: true),
                    RequesterDepartment = table.Column<int>(type: "integer", nullable: true),
                    RequesterBranch = table.Column<int>(type: "integer", nullable: true),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Cancelled = table.Column<string>(type: "text", nullable: true),
                    SalesPersonCode = table.Column<int>(type: "integer", nullable: true),
                    DocumentsOwner = table.Column<int>(type: "integer", nullable: true),
                    TransportationCode = table.Column<int>(type: "integer", nullable: true),
                    DocDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DocDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TaxDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BPLId = table.Column<int>(type: "integer", nullable: true),
                    ContactPersonCode = table.Column<int>(type: "integer", nullable: true),
                    ShipToCode = table.Column<string>(type: "text", nullable: true),
                    RoundingDiffAmount = table.Column<double>(type: "double precision", nullable: true),
                    TotalDiscount = table.Column<double>(type: "double precision", nullable: true),
                    UStage = table.Column<string>(type: "text", nullable: true),
                    UWarehouse = table.Column<string>(type: "text", nullable: true),
                    UOwner = table.Column<string>(type: "text", nullable: true),
                    UPoType = table.Column<string>(type: "text", nullable: true),
                    UTrn = table.Column<string>(type: "text", nullable: true),
                    UDisId = table.Column<string>(type: "text", nullable: true),
                    UDispachAdd = table.Column<string>(type: "text", nullable: true),
                    URemark = table.Column<string>(type: "text", nullable: true),
                    UDispatchTo = table.Column<string>(type: "text", nullable: true),
                    UContactPerson = table.Column<string>(type: "text", nullable: true),
                    UPriceBasis = table.Column<string>(type: "text", nullable: true),
                    UModeOfTransport = table.Column<string>(type: "text", nullable: true),
                    UMatOutDoc = table.Column<string>(type: "text", nullable: true),
                    UGoodsIssue = table.Column<string>(type: "text", nullable: true),
                    UMatInDoc = table.Column<string>(type: "text", nullable: true),
                    UGoodsReceipt = table.Column<string>(type: "text", nullable: true),
                    UDelTerms = table.Column<string>(type: "text", nullable: true),
                    UInspectionBy = table.Column<string>(type: "text", nullable: true),
                    UTransportation = table.Column<string>(type: "text", nullable: true),
                    USupervision = table.Column<string>(type: "text", nullable: true),
                    UTransitIns = table.Column<string>(type: "text", nullable: true),
                    UDrawDocs = table.Column<string>(type: "text", nullable: true),
                    ULoading = table.Column<string>(type: "text", nullable: true),
                    UWarranty = table.Column<string>(type: "text", nullable: true),
                    UUnloading = table.Column<string>(type: "text", nullable: true),
                    UOtherRemark = table.Column<string>(type: "text", nullable: true),
                    UPainting = table.Column<string>(type: "text", nullable: true),
                    UTestCerts = table.Column<string>(type: "text", nullable: true),
                    UPackingForwarding = table.Column<string>(type: "text", nullable: true),
                    UTcDispatchAddress = table.Column<string>(type: "text", nullable: true),
                    SyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestSyncStates",
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
                    LastDocEntry = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestSyncStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PurchaseRequestId = table.Column<int>(type: "integer", nullable: false),
                    LineNum = table.Column<int>(type: "integer", nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ItemDescription = table.Column<string>(type: "text", nullable: true),
                    AccountCode = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<double>(type: "double precision", nullable: true),
                    RequiredDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UnitPrice = table.Column<double>(type: "double precision", nullable: true),
                    DiscountPercent = table.Column<double>(type: "double precision", nullable: true),
                    LineTotal = table.Column<double>(type: "double precision", nullable: true),
                    TaxPercentagePerRow = table.Column<double>(type: "double precision", nullable: true),
                    TaxTotal = table.Column<double>(type: "double precision", nullable: true),
                    TaxCode = table.Column<string>(type: "text", nullable: true),
                    WTLiable = table.Column<string>(type: "text", nullable: true),
                    TaxLiable = table.Column<string>(type: "text", nullable: true),
                    GrossTotal = table.Column<double>(type: "double precision", nullable: true),
                    WarehouseCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    HSNEntry = table.Column<int>(type: "integer", nullable: true),
                    SACEntry = table.Column<int>(type: "integer", nullable: true),
                    UoMCode = table.Column<string>(type: "text", nullable: true),
                    UoMEntry = table.Column<int>(type: "integer", nullable: true),
                    UnitsOfMeasurment = table.Column<double>(type: "double precision", nullable: true),
                    InventoryQuantity = table.Column<double>(type: "double precision", nullable: true),
                    UseBaseUnits = table.Column<string>(type: "text", nullable: true),
                    ProjectCode = table.Column<string>(type: "text", nullable: true),
                    CostingCode = table.Column<string>(type: "text", nullable: true),
                    CostingCode2 = table.Column<string>(type: "text", nullable: true),
                    CostingCode3 = table.Column<string>(type: "text", nullable: true),
                    CostingCode4 = table.Column<string>(type: "text", nullable: true),
                    CostingCode5 = table.Column<string>(type: "text", nullable: true),
                    UProdNo = table.Column<string>(type: "text", nullable: true),
                    BaseType = table.Column<int>(type: "integer", nullable: true),
                    BaseEntry = table.Column<int>(type: "integer", nullable: true),
                    BaseLine = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestLines_PurchaseRequests_PurchaseRequestId",
                        column: x => x.PurchaseRequestId,
                        principalTable: "PurchaseRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseRequestPaymentTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PurchaseRequestId = table.Column<int>(type: "integer", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    Basic = table.Column<int>(type: "integer", nullable: true),
                    Gst = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Stage = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseRequestPaymentTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseRequestPaymentTerms_PurchaseRequests_PurchaseReques~",
                        column: x => x.PurchaseRequestId,
                        principalTable: "PurchaseRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestLines_PurchaseRequestId_LineNum",
                table: "PurchaseRequestLines",
                columns: new[] { "PurchaseRequestId", "LineNum" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestPaymentTerms_PurchaseRequestId_Slot",
                table: "PurchaseRequestPaymentTerms",
                columns: new[] { "PurchaseRequestId", "Slot" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_CompanyDb_CardCode",
                table: "PurchaseRequests",
                columns: new[] { "CompanyDb", "CardCode" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_CompanyDb_DocDate",
                table: "PurchaseRequests",
                columns: new[] { "CompanyDb", "DocDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_CompanyDb_DocEntry",
                table: "PurchaseRequests",
                columns: new[] { "CompanyDb", "DocEntry" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_CompanyDb_DocNum",
                table: "PurchaseRequests",
                columns: new[] { "CompanyDb", "DocNum" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequestSyncStates_CompanyDb",
                table: "PurchaseRequestSyncStates",
                column: "CompanyDb",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseRequestLines");

            migrationBuilder.DropTable(
                name: "PurchaseRequestPaymentTerms");

            migrationBuilder.DropTable(
                name: "PurchaseRequestSyncStates");

            migrationBuilder.DropTable(
                name: "PurchaseRequests");
        }
    }
}
