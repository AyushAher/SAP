using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemLocalStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CompanyDb = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ItemGroupCode = table.Column<int>(type: "integer", nullable: true),
                    ItemsGroupCode = table.Column<int>(type: "integer", nullable: true),
                    InventoryItem = table.Column<string>(type: "text", nullable: true),
                    InventoryUom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PurchaseUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PurchaseItemsPerUnit = table.Column<double>(type: "double precision", nullable: true),
                    InventoryWeight = table.Column<double>(type: "double precision", nullable: true),
                    UoMGroupEntry = table.Column<int>(type: "integer", nullable: true),
                    InventoryUoMEntry = table.Column<int>(type: "integer", nullable: true),
                    DefaultPurchasingUoMEntry = table.Column<int>(type: "integer", nullable: true),
                    ChapterID = table.Column<string>(type: "text", nullable: true),
                    DefaultWarehouse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    GstRelevant = table.Column<string>(type: "text", nullable: true),
                    PurchaseVatGroup = table.Column<string>(type: "text", nullable: true),
                    SyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemSyncStates",
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
                    LastItemCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemSyncStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_CompanyDb_ItemCode",
                table: "Items",
                columns: new[] { "CompanyDb", "ItemCode" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CompanyDb_ItemName",
                table: "Items",
                columns: new[] { "CompanyDb", "ItemName" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_CompanyDb_ItemsGroupCode",
                table: "Items",
                columns: new[] { "CompanyDb", "ItemsGroupCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ItemSyncStates_CompanyDb",
                table: "ItemSyncStates",
                column: "CompanyDb",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "ItemSyncStates");
        }
    }
}
