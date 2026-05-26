using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    public partial class AddWorkflowBusinessAccess : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessName",
                table: "wf_business_binding",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefinitionCode",
                table: "wf_business_binding",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefinitionName",
                table: "wf_business_binding",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "wf_business_binding",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "demo_approval_order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApplicantUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicantUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demo_approval_order", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_demo_approval_order_IsDeleted",
                table: "demo_approval_order",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_demo_approval_order_TenantId",
                table: "demo_approval_order",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_demo_approval_order_TenantId_ApprovalStatus_CreatedAt",
                table: "demo_approval_order",
                columns: new[] { "TenantId", "ApprovalStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_demo_approval_order_TenantId_OrderNo_IsDeleted",
                table: "demo_approval_order",
                columns: new[] { "TenantId", "OrderNo", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_demo_approval_order_TenantId_WorkflowInstanceId",
                table: "demo_approval_order",
                columns: new[] { "TenantId", "WorkflowInstanceId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "demo_approval_order");

            migrationBuilder.DropColumn(
                name: "BusinessName",
                table: "wf_business_binding");

            migrationBuilder.DropColumn(
                name: "DefinitionCode",
                table: "wf_business_binding");

            migrationBuilder.DropColumn(
                name: "DefinitionName",
                table: "wf_business_binding");

            migrationBuilder.DropColumn(
                name: "Remark",
                table: "wf_business_binding");
        }
    }
}
