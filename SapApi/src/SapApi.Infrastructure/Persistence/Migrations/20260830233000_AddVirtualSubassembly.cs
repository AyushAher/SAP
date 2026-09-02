using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SapApi.Infrastructure.Persistence;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260830233000_AddVirtualSubassembly")]
    public partial class AddVirtualSubassembly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVirtualSubassembly",
                table: "ProductionOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentAbsoluteEntry",
                table: "ProductionOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CompanyDb_ParentAbsoluteEntry",
                table: "ProductionOrders",
                columns: new[] { "CompanyDb", "ParentAbsoluteEntry" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_CompanyDb_ParentAbsoluteEntry",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "IsVirtualSubassembly",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ParentAbsoluteEntry",
                table: "ProductionOrders");
        }
    }
}
