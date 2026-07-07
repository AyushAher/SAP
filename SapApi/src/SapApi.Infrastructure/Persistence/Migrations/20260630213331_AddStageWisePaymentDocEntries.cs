using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStageWisePaymentDocEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DownPaymentDocEntry",
                table: "StageWisePayments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentDocEntry",
                table: "StageWisePayments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DownPaymentDocEntry",
                table: "StageWisePayments");

            migrationBuilder.DropColumn(
                name: "PaymentDocEntry",
                table: "StageWisePayments");
        }
    }
}
