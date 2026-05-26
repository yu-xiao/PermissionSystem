using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    public partial class AdjustWorkflowBusinessBindingIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wf_business_binding_TenantId_BusinessType",
                table: "wf_business_binding");

            migrationBuilder.CreateIndex(
                name: "IX_wf_business_binding_TenantId_BusinessType_IsDeleted",
                table: "wf_business_binding",
                columns: new[] { "TenantId", "BusinessType", "IsDeleted" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wf_business_binding_TenantId_BusinessType_IsDeleted",
                table: "wf_business_binding");

            migrationBuilder.CreateIndex(
                name: "IX_wf_business_binding_TenantId_BusinessType",
                table: "wf_business_binding",
                columns: new[] { "TenantId", "BusinessType" },
                unique: true);
        }
    }
}
