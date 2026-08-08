using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EA017WorkflowConcurrencyControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wf_instance_TenantId_BusinessType_BusinessId",
                table: "wf_instance");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "wf_task",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "wf_instance",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "demo_business_order",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "demo_approval_order",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.Sql(
                "IF EXISTS (" +
                "SELECT 1 FROM [wf_instance] " +
                "WHERE [Status] = 0 AND [IsDeleted] = 0 " +
                "GROUP BY [TenantId], [BusinessType], [BusinessId] " +
                "HAVING COUNT(*) > 1) " +
                "THROW 51000, 'EA-017 migration blocked: duplicate running workflow instances must be resolved before creating the unique index.', 1;");

            migrationBuilder.CreateIndex(
                name: "IX_wf_instance_TenantId_BusinessType_BusinessId",
                table: "wf_instance",
                columns: new[] { "TenantId", "BusinessType", "BusinessId" },
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wf_instance_TenantId_BusinessType_BusinessId",
                table: "wf_instance");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "wf_task");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "wf_instance");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "demo_business_order");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "demo_approval_order");

            migrationBuilder.CreateIndex(
                name: "IX_wf_instance_TenantId_BusinessType_BusinessId",
                table: "wf_instance",
                columns: new[] { "TenantId", "BusinessType", "BusinessId" });
        }
    }
}
