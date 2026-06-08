using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermissionSystem.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260526160000_AddReportCenter")]
    public partial class AddReportCenter : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DataSourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SqlText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ColumnsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParamsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportQueryParams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParamCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParamName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParamType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Required = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ReportQueryParams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReportExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExecuteUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExecuteUserName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ParamsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ElapsedMilliseconds = table.Column<long>(type: "bigint", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(name: "IX_ReportDefinitions_IsDeleted", table: "ReportDefinitions", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_ReportDefinitions_TenantId", table: "ReportDefinitions", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_ReportDefinitions_TenantId_Category_IsEnabled", table: "ReportDefinitions", columns: new[] { "TenantId", "Category", "IsEnabled" });
            migrationBuilder.CreateIndex(name: "IX_ReportDefinitions_TenantId_ReportCode", table: "ReportDefinitions", columns: new[] { "TenantId", "ReportCode" }, unique: true);

            migrationBuilder.CreateIndex(name: "IX_ReportQueryParams_IsDeleted", table: "ReportQueryParams", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_ReportQueryParams_TenantId", table: "ReportQueryParams", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_ReportQueryParams_TenantId_ReportId_ParamCode", table: "ReportQueryParams", columns: new[] { "TenantId", "ReportId", "ParamCode" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_ReportQueryParams_TenantId_ReportId_Sort", table: "ReportQueryParams", columns: new[] { "TenantId", "ReportId", "Sort" });

            migrationBuilder.CreateIndex(name: "IX_ReportExecutionLogs_IsDeleted", table: "ReportExecutionLogs", column: "IsDeleted");
            migrationBuilder.CreateIndex(name: "IX_ReportExecutionLogs_TenantId", table: "ReportExecutionLogs", column: "TenantId");
            migrationBuilder.CreateIndex(name: "IX_ReportExecutionLogs_TenantId_ReportCode_CreatedAt", table: "ReportExecutionLogs", columns: new[] { "TenantId", "ReportCode", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_ReportExecutionLogs_TenantId_ReportId_CreatedAt", table: "ReportExecutionLogs", columns: new[] { "TenantId", "ReportId", "CreatedAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReportExecutionLogs");
            migrationBuilder.DropTable(name: "ReportQueryParams");
            migrationBuilder.DropTable(name: "ReportDefinitions");
        }
    }
}
