using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SapApi.Infrastructure.Persistence;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260828133000_AddPurchaseOrderPackingAndTcDispatchUdf")]
    public partial class AddPurchaseOrderPackingAndTcDispatchUdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UPackingForwarding",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UTcDispatchAddress",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UPackingForwarding",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "UTcDispatchAddress",
                table: "PurchaseOrders");
        }
    }
}
