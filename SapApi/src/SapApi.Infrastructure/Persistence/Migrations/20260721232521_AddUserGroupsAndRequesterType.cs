using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGroupsAndRequesterType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "RequesterUserId",
                table: "ApprovalPolicies",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "RequesterGroupId",
                table: "ApprovalPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequesterType",
                table: "ApprovalPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UserGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyDb = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserGroupId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserGroupMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserGroupMembers_UserGroups_UserGroupId",
                        column: x => x.UserGroupId,
                        principalTable: "UserGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_RequesterGroupId",
                table: "ApprovalPolicies",
                column: "RequesterGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMembers_UserGroupId_UserId",
                table: "UserGroupMembers",
                columns: new[] { "UserGroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMembers_UserId",
                table: "UserGroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroups_CompanyDb_Name",
                table: "UserGroups",
                columns: new[] { "CompanyDb", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalPolicies_UserGroups_RequesterGroupId",
                table: "ApprovalPolicies",
                column: "RequesterGroupId",
                principalTable: "UserGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalPolicies_UserGroups_RequesterGroupId",
                table: "ApprovalPolicies");

            migrationBuilder.DropTable(
                name: "UserGroupMembers");

            migrationBuilder.DropTable(
                name: "UserGroups");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicies_RequesterGroupId",
                table: "ApprovalPolicies");

            migrationBuilder.DropColumn(
                name: "RequesterGroupId",
                table: "ApprovalPolicies");

            migrationBuilder.DropColumn(
                name: "RequesterType",
                table: "ApprovalPolicies");

            migrationBuilder.AlterColumn<int>(
                name: "RequesterUserId",
                table: "ApprovalPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
