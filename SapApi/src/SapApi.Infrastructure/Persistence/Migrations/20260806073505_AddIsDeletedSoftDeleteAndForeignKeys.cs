using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SapApi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeletedSoftDeleteAndForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalPolicies_AspNetUsers_ApplicationUserId",
                table: "ApprovalPolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalPolicyApprovers_AspNetUsers_ApplicationUserId",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalRequests_ApprovalPolicies_PolicyId",
                table: "ApprovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalRequests_AspNetUsers_RequesterUserId",
                table: "ApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_UserGroups_CompanyDb_Name",
                table: "UserGroups");

            migrationBuilder.DropIndex(
                name: "IX_UserGroupMembers_UserGroupId_UserId",
                table: "UserGroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_UserGroupMembers_UserId",
                table: "UserGroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_StageWisePaymentBatchLinePaymentTerms_LineId_PaymentTermsTy~",
                table: "StageWisePaymentBatchLinePaymentTerms");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_CompanyDb_DocEntry",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderPaymentTerms_PurchaseOrderId_Slot",
                table: "PurchaseOrderPaymentTerms");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLines_PurchaseOrderId_LineNum",
                table: "PurchaseOrderLines");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicyApprovers_ApplicationUserId",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalPolicies_ApplicationUserId",
                table: "ApprovalPolicies");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "ApprovalPolicies");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "UserGroups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "UserGroupMembers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "UserApprovals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StageWisePayments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StageWisePaymentBatchLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StageWisePaymentBatchLinePaymentTerms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "StageWisePaymentBatches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ReceiptFromProductionRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PurchaseOrderSyncStates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PurchaseOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PurchaseOrderPaymentTerms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PurchaseOrderLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "IssueForProductionRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CacheEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AspNetRoles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ApprovalRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ApprovalPolicyRules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ApprovalPolicyApprovers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ApprovalPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ApprovalLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroups_CompanyDb_Name",
                table: "UserGroups",
                columns: new[] { "CompanyDb", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMembers_UserGroupId_UserId",
                table: "UserGroupMembers",
                columns: new[] { "UserGroupId", "UserId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMembers_UserId",
                table: "UserGroupMembers",
                column: "UserId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePaymentBatchLinePaymentTerms_LineId_PaymentTermsTy~",
                table: "StageWisePaymentBatchLinePaymentTerms",
                columns: new[] { "LineId", "PaymentTermsType" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CompanyDb_DocEntry",
                table: "PurchaseOrders",
                columns: new[] { "CompanyDb", "DocEntry" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderPaymentTerms_PurchaseOrderId_Slot",
                table: "PurchaseOrderPaymentTerms",
                columns: new[] { "PurchaseOrderId", "Slot" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseOrderId_LineNum",
                table: "PurchaseOrderLines",
                columns: new[] { "PurchaseOrderId", "LineNum" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalLogs_ActionByUserId",
                table: "ApprovalLogs",
                column: "ActionByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalLogs_AspNetUsers_ActionByUserId",
                table: "ApprovalLogs",
                column: "ActionByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRequests_ApprovalPolicies_PolicyId",
                table: "ApprovalRequests",
                column: "PolicyId",
                principalTable: "ApprovalPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRequests_AspNetUsers_RequesterUserId",
                table: "ApprovalRequests",
                column: "RequesterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalLogs_AspNetUsers_ActionByUserId",
                table: "ApprovalLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalRequests_ApprovalPolicies_PolicyId",
                table: "ApprovalRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalRequests_AspNetUsers_RequesterUserId",
                table: "ApprovalRequests");

            migrationBuilder.DropIndex(
                name: "IX_UserGroups_CompanyDb_Name",
                table: "UserGroups");

            migrationBuilder.DropIndex(
                name: "IX_UserGroupMembers_UserGroupId_UserId",
                table: "UserGroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_UserGroupMembers_UserId",
                table: "UserGroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_StageWisePaymentBatchLinePaymentTerms_LineId_PaymentTermsTy~",
                table: "StageWisePaymentBatchLinePaymentTerms");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_CompanyDb_DocEntry",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderPaymentTerms_PurchaseOrderId_Slot",
                table: "PurchaseOrderPaymentTerms");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLines_PurchaseOrderId_LineNum",
                table: "PurchaseOrderLines");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalLogs_ActionByUserId",
                table: "ApprovalLogs");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "UserGroups");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "UserGroupMembers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "UserApprovals");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StageWisePayments");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StageWisePaymentBatchLines");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StageWisePaymentBatchLinePaymentTerms");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "StageWisePaymentBatches");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ReceiptFromProductionRequests");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PurchaseOrderSyncStates");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PurchaseOrderPaymentTerms");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "IssueForProductionRequests");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CacheEntries");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ApprovalRequests");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ApprovalPolicyRules");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ApprovalPolicyApprovers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ApprovalPolicies");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ApprovalLogs");

            migrationBuilder.AddColumn<int>(
                name: "ApplicationUserId",
                table: "ApprovalPolicyApprovers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApplicationUserId",
                table: "ApprovalPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroups_CompanyDb_Name",
                table: "UserGroups",
                columns: new[] { "CompanyDb", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMembers_UserGroupId_UserId",
                table: "UserGroupMembers",
                columns: new[] { "UserGroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGroupMembers_UserId",
                table: "UserGroupMembers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StageWisePaymentBatchLinePaymentTerms_LineId_PaymentTermsTy~",
                table: "StageWisePaymentBatchLinePaymentTerms",
                columns: new[] { "LineId", "PaymentTermsType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CompanyDb_DocEntry",
                table: "PurchaseOrders",
                columns: new[] { "CompanyDb", "DocEntry" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderPaymentTerms_PurchaseOrderId_Slot",
                table: "PurchaseOrderPaymentTerms",
                columns: new[] { "PurchaseOrderId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseOrderId_LineNum",
                table: "PurchaseOrderLines",
                columns: new[] { "PurchaseOrderId", "LineNum" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicyApprovers_ApplicationUserId",
                table: "ApprovalPolicyApprovers",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalPolicies_ApplicationUserId",
                table: "ApprovalPolicies",
                column: "ApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalPolicies_AspNetUsers_ApplicationUserId",
                table: "ApprovalPolicies",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalPolicyApprovers_AspNetUsers_ApplicationUserId",
                table: "ApprovalPolicyApprovers",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRequests_ApprovalPolicies_PolicyId",
                table: "ApprovalRequests",
                column: "PolicyId",
                principalTable: "ApprovalPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalRequests_AspNetUsers_RequesterUserId",
                table: "ApprovalRequests",
                column: "RequesterUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
