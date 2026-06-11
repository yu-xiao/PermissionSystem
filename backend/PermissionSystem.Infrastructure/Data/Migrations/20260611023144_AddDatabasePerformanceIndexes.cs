using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabasePerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wf_task_TenantId_ApproverUserId_Status_CreatedAt",
                table: "wf_task");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_UserId_NotificationId",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_sso_login_log_ExternalUserId",
                table: "sso_login_log");

            migrationBuilder.DropIndex(
                name: "IX_sso_login_log_LocalUserId",
                table: "sso_login_log");

            migrationBuilder.DropIndex(
                name: "IX_sso_login_log_TraceId",
                table: "sso_login_log");

            migrationBuilder.DropIndex(
                name: "IX_OperationLogs_TraceId",
                table: "OperationLogs");

            migrationBuilder.DropIndex(
                name: "IX_OperationLogs_UserId",
                table: "OperationLogs");

            migrationBuilder.DropIndex(
                name: "IX_LoginLogs_TraceId",
                table: "LoginLogs");

            migrationBuilder.DropIndex(
                name: "IX_LoginLogs_UserId",
                table: "LoginLogs");

            migrationBuilder.DropIndex(
                name: "IX_FileResources_TenantId_BusinessType_BusinessId",
                table: "FileResources");

            migrationBuilder.CreateIndex(
                name: "IX_wf_task_TenantId_ApproverUserId_Status_AssignedAt",
                table: "wf_task",
                columns: new[] { "TenantId", "ApproverUserId", "Status", "AssignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_wf_task_TenantId_InstanceId_ApproverUserId",
                table: "wf_task",
                columns: new[] { "TenantId", "InstanceId", "ApproverUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Email",
                table: "Users",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_PhoneNumber",
                table: "Users",
                columns: new[] { "TenantId", "PhoneNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_TenantId_UserId_NotificationId",
                table: "UserNotifications",
                columns: new[] { "TenantId", "UserId", "NotificationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_TenantId_LocalUserId_CreatedAt",
                table: "sso_login_log",
                columns: new[] { "TenantId", "LocalUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_TenantId_ProviderCode_ExternalUserId_CreatedAt",
                table: "sso_login_log",
                columns: new[] { "TenantId", "ProviderCode", "ExternalUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_TenantId_TraceId",
                table: "sso_login_log",
                columns: new[] { "TenantId", "TraceId" });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_IsEnabled_Sort",
                table: "Roles",
                columns: new[] { "TenantId", "IsEnabled", "Sort" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExecutionLogs_TenantId_CreatedAt",
                table: "ReportExecutionLogs",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_TenantId_Group",
                table: "Permissions",
                columns: new[] { "TenantId", "Group" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextRetryAt_CreatedAt",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextRetryAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_TenantId_Status_CreatedAt",
                table: "OutboxMessages",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_TenantId_TraceId",
                table: "OperationLogs",
                columns: new[] { "TenantId", "TraceId" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_TenantId_UserId_CreatedAt",
                table: "OperationLogs",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Menus_TenantId_ParentId_Sort",
                table: "Menus",
                columns: new[] { "TenantId", "ParentId", "Sort" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginLogs_TenantId_TraceId",
                table: "LoginLogs",
                columns: new[] { "TenantId", "TraceId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoginLogs_TenantId_UserId_CreatedAt",
                table: "LoginLogs",
                columns: new[] { "TenantId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_TenantId_Status_CreatedAt",
                table: "InboxMessages",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FileResources_TenantId_BusinessType_BusinessId_CreatedAt",
                table: "FileResources",
                columns: new[] { "TenantId", "BusinessType", "BusinessId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalApiCallLogs_TenantId_CreatedAt",
                table: "ExternalApiCallLogs",
                columns: new[] { "TenantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wf_task_TenantId_ApproverUserId_Status_AssignedAt",
                table: "wf_task");

            migrationBuilder.DropIndex(
                name: "IX_wf_task_TenantId_InstanceId_ApproverUserId",
                table: "wf_task");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_PhoneNumber",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_TenantId_UserId_NotificationId",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_sso_login_log_TenantId_LocalUserId_CreatedAt",
                table: "sso_login_log");

            migrationBuilder.DropIndex(
                name: "IX_sso_login_log_TenantId_ProviderCode_ExternalUserId_CreatedAt",
                table: "sso_login_log");

            migrationBuilder.DropIndex(
                name: "IX_sso_login_log_TenantId_TraceId",
                table: "sso_login_log");

            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_IsEnabled_Sort",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_ReportExecutionLogs_TenantId_CreatedAt",
                table: "ReportExecutionLogs");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_TenantId_Group",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_NextRetryAt_CreatedAt",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_TenantId_Status_CreatedAt",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OperationLogs_TenantId_TraceId",
                table: "OperationLogs");

            migrationBuilder.DropIndex(
                name: "IX_OperationLogs_TenantId_UserId_CreatedAt",
                table: "OperationLogs");

            migrationBuilder.DropIndex(
                name: "IX_Menus_TenantId_ParentId_Sort",
                table: "Menus");

            migrationBuilder.DropIndex(
                name: "IX_LoginLogs_TenantId_TraceId",
                table: "LoginLogs");

            migrationBuilder.DropIndex(
                name: "IX_LoginLogs_TenantId_UserId_CreatedAt",
                table: "LoginLogs");

            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_TenantId_Status_CreatedAt",
                table: "InboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_FileResources_TenantId_BusinessType_BusinessId_CreatedAt",
                table: "FileResources");

            migrationBuilder.DropIndex(
                name: "IX_ExternalApiCallLogs_TenantId_CreatedAt",
                table: "ExternalApiCallLogs");

            migrationBuilder.CreateIndex(
                name: "IX_wf_task_TenantId_ApproverUserId_Status_CreatedAt",
                table: "wf_task",
                columns: new[] { "TenantId", "ApproverUserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId_NotificationId",
                table: "UserNotifications",
                columns: new[] { "UserId", "NotificationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_ExternalUserId",
                table: "sso_login_log",
                column: "ExternalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_LocalUserId",
                table: "sso_login_log",
                column: "LocalUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sso_login_log_TraceId",
                table: "sso_login_log",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_TraceId",
                table: "OperationLogs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationLogs_UserId",
                table: "OperationLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginLogs_TraceId",
                table: "LoginLogs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_LoginLogs_UserId",
                table: "LoginLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FileResources_TenantId_BusinessType_BusinessId",
                table: "FileResources",
                columns: new[] { "TenantId", "BusinessType", "BusinessId" });
        }
    }
}
