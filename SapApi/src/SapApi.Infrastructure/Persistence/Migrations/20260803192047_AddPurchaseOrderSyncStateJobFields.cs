using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderSyncStateJobFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LastSyncMessage",
                table: "PurchaseOrderSyncStates",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HangfireJobId",
                table: "PurchaseOrderSyncStates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LastDocEntry",
                table: "PurchaseOrderSyncStates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "PurchaseOrderSyncStates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "PurchaseOrderSyncStates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Idle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HangfireJobId",
                table: "PurchaseOrderSyncStates");

            migrationBuilder.DropColumn(
                name: "LastDocEntry",
                table: "PurchaseOrderSyncStates");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "PurchaseOrderSyncStates");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PurchaseOrderSyncStates");

            migrationBuilder.AlterColumn<string>(
                name: "LastSyncMessage",
                table: "PurchaseOrderSyncStates",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
