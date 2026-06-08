using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    public partial class AddNumberRuleEngine : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NumberRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BusinessType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateFormat = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SequenceLength = table.Column<int>(type: "int", nullable: false, defaultValue: 4),
                    ResetCycle = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Separator = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_NumberRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberRuleSegments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SegmentType = table.Column<int>(type: "int", nullable: false),
                    SegmentValue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Sort = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberRuleSegments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SequenceKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CurrentValue = table.Column<long>(type: "bigint", nullable: false),
                    LastGeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSequences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NumberRules_IsDeleted",
                table: "NumberRules",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_NumberRules_TenantId",
                table: "NumberRules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberRules_TenantId_BusinessType_IsEnabled",
                table: "NumberRules",
                columns: new[] { "TenantId", "BusinessType", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberRules_TenantId_RuleCode",
                table: "NumberRules",
                columns: new[] { "TenantId", "RuleCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumberRuleSegments_IsDeleted",
                table: "NumberRuleSegments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_NumberRuleSegments_TenantId",
                table: "NumberRuleSegments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberRuleSegments_TenantId_RuleId_Sort",
                table: "NumberRuleSegments",
                columns: new[] { "TenantId", "RuleId", "Sort" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_IsDeleted",
                table: "NumberSequences",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_TenantId",
                table: "NumberSequences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NumberSequences_TenantId_RuleCode_SequenceKey",
                table: "NumberSequences",
                columns: new[] { "TenantId", "RuleCode", "SequenceKey" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "NumberRuleSegments");
            migrationBuilder.DropTable(name: "NumberRules");
            migrationBuilder.DropTable(name: "NumberSequences");
        }
    }
}
