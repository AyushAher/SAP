using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptFromProductionRequestListFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserName",
                table: "ReceiptFromProductionRequests",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOnUtc",
                table: "ReceiptFromProductionRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<string>(
                name: "WorkerName",
                table: "ReceiptFromProductionRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedByUserName",
                table: "ReceiptFromProductionRequests");

            migrationBuilder.DropColumn(
                name: "CreatedOnUtc",
                table: "ReceiptFromProductionRequests");

            migrationBuilder.DropColumn(
                name: "WorkerName",
                table: "ReceiptFromProductionRequests");
        }
    }
}
