using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionOrderParentUdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParentProductionOrderNo",
                table: "ProductionOrders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_CompanyDb_ParentProductionOrderNo",
                table: "ProductionOrders",
                columns: new[] { "CompanyDb", "ParentProductionOrderNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_CompanyDb_ParentProductionOrderNo",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ParentProductionOrderNo",
                table: "ProductionOrders");
        }
    }
}
