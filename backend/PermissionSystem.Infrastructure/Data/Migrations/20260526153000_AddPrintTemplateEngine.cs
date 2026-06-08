using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    public partial class AddPrintTemplateEngine : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrintTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TemplateType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ContentHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaperSize = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Orientation = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Remark = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrintRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BusinessId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrintUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrintUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrintedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PrintCount = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(name: "IX_PrintTemplates_IsDeleted", table: "PrintTemplates", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_PrintTemplates_TenantId", table: "PrintTemplates", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_PrintTemplates_TenantId_BusinessType_IsDefault", table: "PrintTemplates", columns: new[] { "TenantId", "BusinessType", "IsDefault" });
            migrationBuilder.CreateIndex(name: "IX_PrintTemplates_TenantId_BusinessType_TemplateType_IsEnabled", table: "PrintTemplates", columns: new[] { "TenantId", "BusinessType", "TemplateType", "IsEnabled" });
            migrationBuilder.CreateIndex(name: "IX_PrintTemplates_TenantId_TemplateCode", table: "PrintTemplates", columns: new[] { "TenantId", "TemplateCode" }, unique: true);

            migrationBuilder.CreateIndex(name: "IX_PrintRecords_IsDeleted", table: "PrintRecords", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_PrintRecords_TenantId", table: "PrintRecords", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_PrintRecords_TenantId_BusinessType_BusinessId_PrintedAt", table: "PrintRecords", columns: new[] { "TenantId", "BusinessType", "BusinessId", "PrintedAt" });
            migrationBuilder.CreateIndex(name: "IX_PrintRecords_TenantId_TemplateId_PrintedAt", table: "PrintRecords", columns: new[] { "TenantId", "TemplateId", "PrintedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PrintRecords");
            migrationBuilder.DropTable(name: "PrintTemplates");
        }
    }
}
