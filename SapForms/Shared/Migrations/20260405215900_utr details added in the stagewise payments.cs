using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapForm.Migrations
{
    /// <inheritdoc />
    public partial class utrdetailsaddedinthestagewisepayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bank",
                table: "StageWisePayments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UtrDate",
                table: "StageWisePayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UtrNo",
                table: "StageWisePayments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bank",
                table: "StageWisePayments");

            migrationBuilder.DropColumn(
                name: "UtrDate",
                table: "StageWisePayments");

            migrationBuilder.DropColumn(
                name: "UtrNo",
                table: "StageWisePayments");
        }
    }
}
