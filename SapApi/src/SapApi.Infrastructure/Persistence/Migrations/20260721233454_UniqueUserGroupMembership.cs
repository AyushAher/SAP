using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUserGroupMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserGroupMembers_UserId",
                table: "UserGroupMembers");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMembers_UserId",
                table: "UserGroupMembers",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserGroupMembers_UserId",
                table: "UserGroupMembers");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMembers_UserId",
                table: "UserGroupMembers",
                column: "UserId");
        }
    }
}
