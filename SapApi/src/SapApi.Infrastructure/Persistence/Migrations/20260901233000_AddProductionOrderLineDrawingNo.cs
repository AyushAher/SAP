using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SapApi.Infrastructure.Persistence;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260901233000_AddProductionOrderLineDrawingNo")]
    public partial class AddProductionOrderLineDrawingNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DrawingNo",
                table: "ProductionOrderLines",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DocNum",
                table: "ProductionOrderLines",
                type: "character varying(25)",
                maxLength: 25,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DrawingNo",
                table: "ProductionOrderLines");

            migrationBuilder.AlterColumn<string>(
                name: "DocNum",
                table: "ProductionOrderLines",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(25)",
                oldMaxLength: 25,
                oldNullable: true);
        }
    }
}
