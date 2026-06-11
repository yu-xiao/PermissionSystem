using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    public partial class AddDemoBusinessOrder : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demo_business_order",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    WorkflowInstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ChangeHistoryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demo_business_order", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_demo_business_order_IsDeleted",
                table: "demo_business_order",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_demo_business_order_TenantId",
                table: "demo_business_order",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_demo_business_order_TenantId_ApprovalStatus_CreatedAt",
                table: "demo_business_order",
                columns: new[] { "TenantId", "ApprovalStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_demo_business_order_TenantId_DepartmentId_CreatedAt",
                table: "demo_business_order",
                columns: new[] { "TenantId", "DepartmentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_demo_business_order_TenantId_OrderNo_IsDeleted",
                table: "demo_business_order",
                columns: new[] { "TenantId", "OrderNo", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_demo_business_order_TenantId_OwnerUserId_CreatedAt",
                table: "demo_business_order",
                columns: new[] { "TenantId", "OwnerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_demo_business_order_TenantId_WorkflowInstanceId",
                table: "demo_business_order",
                columns: new[] { "TenantId", "WorkflowInstanceId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "demo_business_order");
        }
    }
}
